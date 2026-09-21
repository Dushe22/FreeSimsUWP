[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$referenceRoot = Join-Path $tempRoot 'freesims-net45-references/Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3/build'
if (-not (Test-Path $referenceRoot)) { throw 'Run scripts/Build-DesktopBaseline.ps1 first to restore the .NET 4.5 reference assemblies.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
& $msbuild (Join-Path $repoRoot 'tests/FilesCompatibility/FilesCompatibility.csproj') /p:Configuration=Release "/p:TargetFrameworkRootPath=$referenceRoot\" /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Compatibility test compilation failed.' }
& (Join-Path $repoRoot 'tests/FilesCompatibility/bin/Release/FilesCompatibility.exe')
if ($LASTEXITCODE -ne 0) { throw 'Compatibility tests failed.' }