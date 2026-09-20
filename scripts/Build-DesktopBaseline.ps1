[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$NuGetPath = 'nuget'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/desktop-baseline' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Visual Studio Installer / vswhere is required.' }
if (-not (Get-Command $NuGetPath -ErrorAction SilentlyContinue)) { throw 'NuGet CLI is required. Install nuget.exe on PATH or pass -NuGetPath.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }

# VS2022 does not ship .NET 4.5 targeting assemblies. Restore references only;
# do not retarget the desktop source or mistake missing SDKs for engine failures.
$tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$references = Join-Path $tempRoot 'freesims-net45-references'
& $NuGetPath install Microsoft.NETFramework.ReferenceAssemblies.net45 -Version 1.0.3 -OutputDirectory $references -NonInteractive -Source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'Failed to restore .NET 4.5 reference assemblies.' }
$referenceRoot = Join-Path $references 'Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3/build'
# Use solution mappings: client x86, libraries AnyCPU. Build only the client closure.
& $msbuild (Join-Path $repoRoot 'SimsVille/SimsVille.sln') /t:SimsVille /m /verbosity:minimal `
    /p:Configuration=Release /p:Platform=x86 "/p:TargetFrameworkRootPath=$referenceRoot\" `
    "/bl:$OutputDirectory/desktop.binlog" 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Desktop Release/x86 compilation failed with exit $LASTEXITCODE; inspect baseline logs separately from UWP." }
'Desktop Release/x86 compiled. Runtime launch and Sims data loading are NOT tested.' |
    Set-Content (Join-Path $OutputDirectory 'RESULT.txt')
