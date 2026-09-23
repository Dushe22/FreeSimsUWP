[CmdletBinding()]
param(
    [string]$GameRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'GameData\TheSims')
)
# Read-only layout/header check, NOT proof that a game installation is playable.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$issues = [Collections.Generic.List[string]]::new()
$files = [Collections.Generic.List[object]]::new()
$pending = [Collections.Generic.Stack[string]]::new()
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    $issues.Add('Game folder does not exist. Copy the installed game here, retaining its directory structure.')
} else {
    # Reject links in the root ancestry as well as in its descendants.
    $ancestor = Get-Item -LiteralPath $root -Force
    while ($null -ne $ancestor) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Use an ordinary local folder, not a directory link: $($ancestor.FullName)"
        }
        $ancestor = $ancestor.Parent
    }
    $pending.Push($root)
}
while ($pending.Count -gt 0) {
    $directory = $pending.Pop()
    foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            $issues.Add("Skipped link: $($item.FullName.Substring($root.Length))")
        } elseif ($item.PSIsContainer) {
            $pending.Push($item.FullName)
        } else {
            $files.Add($item)
        }
    }
}
$relativeFiles = @{}
foreach ($file in $files) { $relativeFiles[$file.FullName.Substring($root.Length).Replace('\','/')] = $file }
foreach ($required in @('GameData/Objects/Objects.far', 'UserData/Neighborhood.iff')) {
    if (-not $relativeFiles.ContainsKey($required)) { $issues.Add("Missing: $required") }
}
$archives = @($files | Where-Object Extension -eq '.far')
foreach ($file in $archives) {
    $reader = $null
    try {
        $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($file.FullName))
        if ($reader.BaseStream.Length -lt 20) { throw 'Truncated FAR header.' }
        if ([Text.Encoding]::ASCII.GetString($reader.ReadBytes(8)) -cne 'FAR!byAZ') { throw 'Invalid FAR signature.' }
        if ($reader.ReadUInt32() -ne 1) { throw 'Expected FAR version 1.' }
        $offset = $reader.ReadUInt32()
        if ($offset -lt 16 -or $offset -gt ($reader.BaseStream.Length - 4)) { throw 'FAR manifest offset is outside the file.' }
        $reader.BaseStream.Position = $offset
        $count = $reader.ReadUInt32()
        # FAR1b entries need at least 14 bytes before their filename.
        if ($count -gt [Math]::Floor(($reader.BaseStream.Length - $offset - 4) / 14)) { throw 'FAR manifest entry count exceeds available bytes.' }
    } catch {
        $issues.Add("$($file.FullName.Substring($root.Length)): $($_.Exception.Message)")
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
    }
}
$neighborhood = $relativeFiles['UserData/Neighborhood.iff']
if ($null -ne $neighborhood) {
    $reader = $null
    try {
        $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($neighborhood.FullName))
        if ($reader.BaseStream.Length -lt 64) { throw 'Truncated IFF header.' }
        $signature = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(60)).TrimEnd([char]0)
        if ($signature -cne 'IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1' -and
            $signature -cne 'IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1') { throw 'Invalid IFF signature.' }
    } catch {
        $issues.Add("UserData/Neighborhood.iff: $($_.Exception.Message)")
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
    }
}
[pscustomobject]@{
    Root = $root
    FileCount = $files.Count
    FarCount = $archives.Count
    HouseCount = @($relativeFiles.Keys | Where-Object { $_ -match '^UserData/Houses/House[^/]*\.iff$' }).Count
    ExpansionFolders = @($relativeFiles.Keys | ForEach-Object { ($_ -split '/')[0] } |
        Where-Object { $_ -match '^(ExpansionPack\d*|Deluxe)$' } | Sort-Object -Unique)
    LayoutAndHeadersPass = ($issues.Count -eq 0)
    Issues = $issues.ToArray()
    Scope = 'Layout and header checks only; archive entries, resources and gameplay still require validation. No files changed.'
}

