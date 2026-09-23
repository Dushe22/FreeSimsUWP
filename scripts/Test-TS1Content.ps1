[CmdletBinding()]
param([string]$GameRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'GameData\TheSims'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Test-FilesCompatibility.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Synthetic parser checks failed.' }
$inputs = @('GameData/Objects/Objects.far','UserData/Neighborhood.iff','UserData/Houses/House01.iff')
$before = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $GameRoot $_) -Algorithm SHA256).Hash })
& (Join-Path $repo 'tests/FilesCompatibility/bin/Release/FilesCompatibility.exe') $GameRoot (Join-Path $repo 'artifacts/content-desktop')
$testExit = $LASTEXITCODE
$after = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $GameRoot $_) -Algorithm SHA256).Hash })
if (($before -join ',') -ne ($after -join ',')) { throw 'Source file hashes changed.' }
Write-Output 'PASS all three source SHA256 hashes unchanged'
if ($testExit -ne 0) { throw 'Real-data parser/storage checks failed.' }
