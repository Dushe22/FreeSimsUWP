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
$inputs = @('UserData/Neighborhood.iff','UserData/Houses/House01.iff')
$before = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $GameRoot $_)).Hash })
& (Join-Path $repo 'tests/OfflineCompatibility/bin/Release/OfflineCompatibility.exe') $GameRoot (Join-Path $repo 'artifacts/offline-desktop')
$testExit = $LASTEXITCODE
$after = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $GameRoot $_)).Hash })
if (($before -join ',') -ne ($after -join ',')) { throw 'Source hashes changed.' }
Write-Output 'PASS both source SHA256 hashes unchanged'
if ($testExit -ne 0) { throw 'Offline checks failed.' }
