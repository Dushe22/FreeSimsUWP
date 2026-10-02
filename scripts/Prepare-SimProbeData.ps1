[CmdletBinding()]
param([string]$GameRoot,[string]$SourceLog,[string]$BaselineRoot,[string]$Destination)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(!$GameRoot){$GameRoot=Join-Path (Split-Path $repo -Parent) 'GameData/TheSims'}
if(!$SourceLog){$SourceLog=Join-Path $repo 'artifacts/sims-qa.log'}
if(!$BaselineRoot){$BaselineRoot=Join-Path $repo 'artifacts/saved-lot-upload/GameData'}
if(!$Destination){$Destination=Join-Path $repo 'artifacts/sim-probe-upload'}
$sourceRoot=[IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')+'\'
$destinationRoot=[IO.Path]::GetFullPath($Destination)
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\','/')+'\'
if(!$destinationRoot.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Destination must remain inside this workspace artifacts directory.'}
$log=@(Get-Content -LiteralPath $SourceLog)
if(@($log | Where-Object {$_ -like 'SIM QA *'}).Count -ne 8 -or @($log | Where-Object {$_ -match '^FAIL|Unhandled exception|SIM INSPECT FAILED'}).Count){throw 'Expected a complete successful eight-view Sim QA log.'}
$sources=@($log | Where-Object {$_ -like 'SIM SOURCE *'} | ForEach-Object {$_.Substring(11)})
if($sources.Count -lt 6){throw 'Run Test-LotRendering -SimsQa successfully before preparing the asset delta.'}
$sources+=Join-Path $sourceRoot 'UserData/Houses/House05.iff'
$manifest=@()
foreach($source in ($sources | Sort-Object -Unique)) {
    $absolute=[IO.Path]::GetFullPath($source)
    if(!$absolute.StartsWith($sourceRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Avatar source lies outside the selected installation.'}
    if((Get-Item -LiteralPath $absolute).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Cannot copy linked game assets.'}
    $relative=$absolute.Substring($sourceRoot.Length)
    $hash=(Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash
    $baseline=Join-Path $BaselineRoot $relative
    if((Test-Path -LiteralPath $baseline) -and (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash -eq $hash){continue}
    $target=Join-Path (Join-Path $destinationRoot 'GameData') $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $absolute -Destination $target -Force
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw 'Copied asset SHA256 differs from original.'}
    $manifest+=@{Path=('GameData/'+$relative.Replace('\','/'));Bytes=(Get-Item -LiteralPath $absolute).Length;SHA256=$hash}
}
if($manifest.Count -eq 0){throw 'No additional assets are needed for this baseline.'}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destinationRoot 'ASSETS.json')
'Private copies of your own game data. Upload the GameData contents separately; never package these assets in APPX or commit/distribute them.' | Set-Content -LiteralPath (Join-Path $destinationRoot 'README.txt')
$zip=$destinationRoot+'.zip'
Compress-Archive -LiteralPath (Join-Path $destinationRoot 'GameData'),(Join-Path $destinationRoot 'ASSETS.json'),(Join-Path $destinationRoot 'README.txt') -DestinationPath $zip -Force
Write-Output "PASS $($manifest.Count) original files copied with matching SHA256; source installation unchanged."
Get-FileHash -LiteralPath $zip -Algorithm SHA256
Write-Output "PRIVATE ASSET DELTA $zip"
