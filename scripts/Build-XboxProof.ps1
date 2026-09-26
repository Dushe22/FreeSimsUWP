[CmdletBinding()]
param(
    [ValidateSet('Proof', 'FilesProbe', 'CommonProbe', 'RuntimeProbe', 'ContentProbe', 'OfflineProbe')]
    [string]$Target = 'Proof',
    [string]$SourceCommit,
    [string]$OutputDirectory,
    [string]$WindowsSdkVersion = '10.0.19041.0',
    [switch]$Sign
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$projectName = switch ($Target) { 'Proof' { 'XboxUwpProof' } 'FilesProbe' { 'XboxFilesProbe' } 'CommonProbe' { 'XboxCommonProbe' } 'RuntimeProbe' { 'XboxRuntimeProbe' } 'ContentProbe' { 'XboxContentProbe' } 'OfflineProbe' { 'XboxOfflineProbe' } }
$artifactName = switch ($Target) { 'Proof' { 'proof' } 'FilesProbe' { 'files-probe' } 'CommonProbe' { 'common-probe' } 'RuntimeProbe' { 'runtime-probe' } 'ContentProbe' { 'content-probe' } 'OfflineProbe' { 'offline-probe' } }
$projectDir = Join-Path $repoRoot ('experiments/' + $projectName)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts/' + $artifactName) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $SourceCommit) { $SourceCommit = (git -C $repoRoot rev-parse HEAD).Trim() }
if ($SourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'SourceCommit must be a full Git commit hash.' }
$headCommit = (git -C $repoRoot rev-parse HEAD).Trim()
$dirty = @(git -C $repoRoot status --porcelain).Count -gt 0
if ($Sign -and ($dirty -or $SourceCommit -ne $headCommit)) {
    throw 'Signed handoffs require a clean checkout and SourceCommit equal to HEAD.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Visual Studio Installer / vswhere is required.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
$sdkBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits/10/bin/$WindowsSdkVersion/x64"
if (-not (Test-Path (Join-Path $sdkBin 'makeappx.exe'))) { throw "Install Windows SDK $WindowsSdkVersion." }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# These are original geometric package logos, generated only on the build host.
# System.Drawing here is a BUILD TOOL dependency; it is not referenced by the app.
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $projectDir 'Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
foreach ($item in @(
    @{ Name = 'StoreLogo.png'; W = 50; H = 50 },
    @{ Name = 'Square44x44Logo.png'; W = 44; H = 44 },
    @{ Name = 'Square150x150Logo.png'; W = 150; H = 150 },
    @{ Name = 'SplashScreen.png'; W = 620; H = 300 }
)) {
    $bitmap = [Drawing.Bitmap]::new($item.W, $item.H)
    $drawing = [Drawing.Graphics]::FromImage($bitmap)
    $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(100, 149, 237))
    try {
        $drawing.Clear([Drawing.Color]::FromArgb(16, 24, 39))
        $size = [int]([Math]::Min($item.W, $item.H) * 0.55)
        $drawing.FillRectangle($brush, [int](($item.W - $size) / 2), [int](($item.H - $size) / 2), $size, $size)
        $bitmap.Save((Join-Path $assets $item.Name), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $brush.Dispose(); $drawing.Dispose(); $bitmap.Dispose() }
}
$identity = 'namespace FreeSims.Xbox.Proof { internal static class BuildInfo { public const string Commit = "' + $SourceCommit + '"; } }'
Set-Content -Path (Join-Path $projectDir 'Properties/BuildInfo.g.cs') -Value $identity -Encoding utf8

$packageDir = Join-Path $OutputDirectory 'AppPackages'
$arguments = @(
    (Join-Path $projectDir ($projectName + '.csproj')), '/restore', '/m', '/verbosity:minimal',
    '/p:Configuration=Release', '/p:Platform=x64',
    "/p:TargetPlatformVersion=$WindowsSdkVersion", '/p:UseDotNetNativeToolchain=true',
    '/p:AppxBundle=Never', '/p:UapAppxPackageBuildMode=SideloadOnly',
    '/p:GenerateAppxPackageOnBuild=true', '/p:AppxPackageSigningEnabled=false',
    "/p:AppxPackageDir=$packageDir\", "/bl:$OutputDirectory/$artifactName.binlog"
)
& $msbuild @arguments 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Release/x64 UWP build failed with exit $LASTEXITCODE." }
$packages = @(Get-ChildItem $packageDir -Recurse -Filter '*.appx' | Where-Object { $_.FullName -notmatch '[\\/]Dependencies[\\/]' })
if ($packages.Count -ne 1) { throw "Expected exactly one application .appx, found $($packages.Count)." }

if ($Sign) {
    # Private key lives only in the build machine's current-user certificate store.
    # Never export a PFX, print a password, or include a private key in artifacts.
    $cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=FreeSimsXboxDevelopment' `
        -KeyUsage DigitalSignature -FriendlyName 'FreeSims Xbox CI test signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddMonths(6) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    try {
        $signTool = Join-Path $sdkBin 'signtool.exe'
        & $signTool sign /fd SHA256 /sha1 $cert.Thumbprint $packages[0].FullName
        if ($LASTEXITCODE -ne 0) { throw 'AppX signing failed.' }
        Export-Certificate -Cert $cert -FilePath (Join-Path $OutputDirectory 'FreeSimsXboxDevelopment.cer') | Out-Null
    } finally {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey
    }
}

$hash = (Get-FileHash $packages[0].FullName -Algorithm SHA256).Hash
@"
Target: $Target
Source commit: $SourceCommit
Uncommitted source changes: $dirty
Configuration: Release / x64 / UWP / .NET Native
MonoGame.Framework.WindowsUniversal: 3.8.1.303
Windows SDK: $WindowsSdkVersion
Signed test package: $($Sign.IsPresent)
Application package: $($packages[0].Name)
Application SHA256: $hash
Hardware verified: NO
See docs/XBOX_PORT.md in this exact source commit for the test procedure.
"@ | Set-Content -Path (Join-Path $OutputDirectory 'BUILD.txt') -Encoding utf8
Write-Host "Package produced: $($packages[0].Name); hardware testing still required."
