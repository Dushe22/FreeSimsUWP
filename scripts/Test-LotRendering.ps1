[CmdletBinding()]
param([string]$GameRoot,[switch]$RebuildShader)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    if(-not $GameRoot){$GameRoot=Join-Path $repo 'artifacts/saved-lot-upload/GameData'}
    if($RebuildShader){
        $compiler=Join-Path $repo 'artifacts/mgfxc/mgfxc.exe'
        if(-not(Test-Path -LiteralPath $compiler)){throw 'Install dotnet-mgfxc 3.8.1.303 into artifacts/mgfxc first.'}
        $oldRollForward=$env:DOTNET_ROLL_FORWARD
        try {$env:DOTNET_ROLL_FORWARD='Major'; & $compiler experiments/XboxOfflineProbe/Effects/TS1SpriteDepth.fx experiments/XboxOfflineProbe/Effects/TS1SpriteDepth.mgfxo /Profile:DirectX_11}
        finally {$env:DOTNET_ROLL_FORWARD=$oldRollForward}
        if($LASTEXITCODE -ne 0){throw 'Shader compilation failed'}
    }
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    $refs=Join-Path ([IO.Path]::GetTempPath()) 'freesims-net45-references/Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3/build'
    & $msbuild SimsVille/SimsVille.csproj /p:Configuration=Release /p:Platform=x86 /p:PlatformTarget=AnyCPU /p:AllowUnsafeBlocks=true /p:Prefer32Bit=false /p:OutputType=Library /p:StartupObject= "/p:OutputPath=$repo\artifacts\lot-check-engine\" "/p:TargetFrameworkRootPath=$refs\" /verbosity:minimal
    if($LASTEXITCODE -ne 0){throw 'Render harness engine compilation failed'}
    & dotnet run --project experiments/DesktopLotRenderCheck -c Release -- ([IO.Path]::GetFullPath($GameRoot))
    if($LASTEXITCODE -ne 0){throw 'Lot GPU checks failed'}
}finally{Pop-Location}
