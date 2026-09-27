[CmdletBinding()]
param(
    [string]$GameRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'GameData\TheSims'),
    [string]$NuGetPath = 'nuget'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Build-DesktopBaseline.ps1') -NuGetPath $NuGetPath
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$references = Join-Path $tempRoot 'freesims-net45-references/Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3/build'
& $msbuild (Join-Path $repo 'tests/OfflineCompatibility/OfflineCompatibility.csproj') "/p:TargetFrameworkRootPath=$references\" /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Offline test build failed.' }
# Desktop MonoGame resolves these assemblies when VM entity ticks are JIT-compiled,
# even for a headless run. They are test dependencies, never Xbox package inputs.
$testPackages = Join-Path $repo 'artifacts/offline-test-packages'
& $NuGetPath install SharpDX.Direct3D11 -Version 4.0.1 -OutputDirectory $testPackages -NonInteractive -Source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'Failed to restore desktop tick dependencies.' }
foreach ($package in @('SharpDX','SharpDX.DXGI','SharpDX.Direct3D11')) {
    Copy-Item -LiteralPath (Join-Path $testPackages "$package.4.0.1/lib/net45/$package.dll") -Destination (Join-Path $repo 'tests/OfflineCompatibility/bin/Release') -Force
}
$inputs = @('UserData/Neighborhood.iff','UserData/Houses/House01.iff','UserData/Houses/House02.iff','UserData/Houses/House28.iff','GameData/Objects/Objects.far','GameData/Global/Global.far')
# Include optional VM content sources; the tests must not modify installed assets.
foreach ($root in @('Deluxe','ExpansionShared','ExpansionPack','ExpansionPack2','ExpansionPack3','ExpansionPack4','ExpansionPack5','ExpansionPack6','ExpansionPack7','Downloads')) {
    $directory = Join-Path $GameRoot $root
    if (Test-Path -LiteralPath $directory) {
        $inputs += @(Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object { $_.Extension -in '.far','.iff' } | ForEach-Object { $_.FullName.Substring($GameRoot.TrimEnd('\','/').Length + 1) })
    }
}
$before = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $GameRoot $_)).Hash })
& (Join-Path $repo 'tests/OfflineCompatibility/bin/Release/OfflineCompatibility.exe') $GameRoot (Join-Path $repo 'artifacts/offline-desktop')
$testExit = $LASTEXITCODE
$after = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $GameRoot $_)).Hash })
if (($before -join ',') -ne ($after -join ',')) { throw 'Source hashes changed.' }
Write-Output "PASS all $($inputs.Count) source SHA256 hashes unchanged"
if ($testExit -ne 0) { throw 'Offline checks failed.' }
