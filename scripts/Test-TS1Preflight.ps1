[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$check = Join-Path $PSScriptRoot 'Test-TS1Installation.ps1'
$fixture = Join-Path $repo ('artifacts/ts1-preflight-tests/' + [Guid]::NewGuid().ToString('N'))
function Require($condition, $message) { if (-not $condition) { throw $message } }
$result = & $check -GameRoot $fixture
Require (-not $result.LayoutAndHeadersPass) 'Missing folder was accepted.'
Require (-not (Test-Path -LiteralPath $fixture)) 'Checker created the missing folder.'
Write-Output 'PASS missing root is read-only'
New-Item -ItemType Directory -Path (Join-Path $fixture 'GameData/Objects') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $fixture 'UserData/Houses') -Force | Out-Null
$result = & $check -GameRoot $fixture
Require ($result.Issues.Count -eq 2) 'Required files were not diagnosed.'
Write-Output 'PASS missing required files'
$farPath = Join-Path $fixture 'GameData/Objects/Objects.far'
$iffPath = Join-Path $fixture 'UserData/Neighborhood.iff'
$far = New-Object byte[] 20
[Text.Encoding]::ASCII.GetBytes('FAR!byAZ').CopyTo($far,0)
[BitConverter]::GetBytes([uint32]1).CopyTo($far,8)
[BitConverter]::GetBytes([uint32]16).CopyTo($far,12)
[IO.File]::WriteAllBytes($farPath,$far)
$iff = New-Object byte[] 64
[Text.Encoding]::ASCII.GetBytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1').CopyTo($iff,0)
[IO.File]::WriteAllBytes($iffPath,$iff)
$before = @((Get-FileHash -LiteralPath $farPath).Hash, (Get-FileHash -LiteralPath $iffPath).Hash)
$result = & $check -GameRoot $fixture
Require ($result.LayoutAndHeadersPass -and $result.FarCount -eq 1) 'Valid synthetic headers rejected.'
$after = @((Get-FileHash -LiteralPath $farPath).Hash, (Get-FileHash -LiteralPath $iffPath).Hash)
Require (($before -join ',') -eq ($after -join ',')) 'Checker modified game bytes.'
Write-Output 'PASS valid headers and unchanged input hashes'
[BitConverter]::GetBytes([uint32]9000).CopyTo($far,12)
[IO.File]::WriteAllBytes($farPath,$far)
$result = & $check -GameRoot $fixture
Require (-not $result.LayoutAndHeadersPass -and ($result.Issues -join ' ') -match 'offset') 'Invalid manifest offset accepted.'
Write-Output 'PASS malformed FAR offset'
[BitConverter]::GetBytes([uint32]16).CopyTo($far,12)
[BitConverter]::GetBytes([uint32]1).CopyTo($far,16)
[IO.File]::WriteAllBytes($farPath,$far)
$result = & $check -GameRoot $fixture
Require (-not $result.LayoutAndHeadersPass -and ($result.Issues -join ' ') -match 'entry count') 'Truncated manifest accepted.'
Write-Output 'PASS truncated FAR manifest'
[BitConverter]::GetBytes([uint32]0).CopyTo($far,16)
[IO.File]::WriteAllBytes($farPath,$far)
[IO.File]::WriteAllBytes($iffPath,[byte[]]@(1,2,3))
$result = & $check -GameRoot $fixture
Require (-not $result.LayoutAndHeadersPass -and ($result.Issues -join ' ') -match 'Truncated IFF') 'Truncated neighborhood accepted.'
Write-Output 'PASS truncated neighborhood'
Write-Output 'RESULT 6/6 PASS (synthetic headers only, no copyrighted data)'

