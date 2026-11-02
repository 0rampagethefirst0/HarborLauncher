param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.14.2')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$zip = Join-Path $root ('Harbor-v' + $Version + '.zip')
Add-Type -AssemblyName System.IO.Compression
$paths = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
function Add-ReleaseFile([string]$relative) {
    $source = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $source.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Release input left the workspace.' }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Missing release input: ' + $relative) }
    $paths['Harbor/' + $relative.Replace('\','/')] = $source
}
foreach ($file in @('README.md','INSTALL.md','CHANGELOG.md','FILES.md','MODS.md','BEHAVIOR.md','DEVELOPMENT.md','build.ps1','NuGet.Config','Restore Windows.cmd')) { Add-ReleaseFile $file }
foreach ($folder in @('App','Session','SessionSource','src','tools','tools\RuntimeCheck','docs')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root $folder) -File) { Add-ReleaseFile (Join-Path $folder $file.Name) }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'src\Previews') -File) { Add-ReleaseFile (Join-Path 'src\Previews' $file.Name) }
foreach ($file in @('tokyo-dragon-outline.png','tokyo-dragon-prompt.txt')) { Add-ReleaseFile (Join-Path 'src\Assets' $file) }
$short = ([version]$Version).ToString(2)
foreach ($name in @('verification','offline','visual','community','runtime')) {
    $file = "checks\$name-v$Version.json"
    if (-not (Test-Path -LiteralPath (Join-Path $root $file))) { $file = "checks\$name-v$short.json" }
    Add-ReleaseFile $file
}
foreach ($file in @('recovery-v0.4.json','runtime-v0.4.json','heartbeat-v0.4.json')) { if (Test-Path -LiteralPath (Join-Path $root ('checks\' + $file))) { Add-ReleaseFile ('checks\' + $file) } }
foreach ($relative in @(& git -C (Join-Path $root 'Community') ls-files)) { Add-ReleaseFile ('Community\' + $relative) }
# Stream verified inputs directly without retaining another full staged copy.
$stream = [IO.File]::Open($zip, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($pair in $paths.GetEnumerator()) {
            $entry = $archive.CreateEntry($pair.Key, [IO.Compression.CompressionLevel]::Optimal)
            $stamp = [IO.File]::GetLastWriteTimeUtc($pair.Value)
            if ($stamp.Year -lt 1980 -or $stamp.Year -gt 2107) { $stamp = [datetime]'1980-01-02' }
            $entry.LastWriteTime = [DateTimeOffset]::new($stamp)
            $input = [IO.File]::OpenRead($pair.Value)
            try { $output = $entry.Open(); try { $input.CopyTo($output) } finally { $output.Dispose() } } finally { $input.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
Write-Output $zip
