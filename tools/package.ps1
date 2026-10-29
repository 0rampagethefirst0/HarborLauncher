param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.8.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root ('Package\Harbor-' + $Version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '\Harbor')
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($folder in @('App','src','tools','checks')) { New-Item -ItemType Directory -Path (Join-Path $stage $folder) -Force | Out-Null }
foreach ($file in @('README.md','MODS.md','BEHAVIOR.md','DEVELOPMENT.md','build.ps1','NuGet.Config','Restore Windows.cmd')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage }
Get-ChildItem -LiteralPath (Join-Path $root 'App') -File | Copy-Item -Destination (Join-Path $stage 'App')
Get-ChildItem -LiteralPath (Join-Path $root 'src') -File | Copy-Item -Destination (Join-Path $stage 'src')
Copy-Item -LiteralPath (Join-Path $root 'src\Previews') -Destination (Join-Path $stage 'src') -Recurse
Get-ChildItem -LiteralPath $PSScriptRoot -File | Copy-Item -Destination (Join-Path $stage 'tools')
New-Item -ItemType Directory -Path (Join-Path $stage 'tools\RuntimeCheck') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'RuntimeCheck') -File | Copy-Item -Destination (Join-Path $stage 'tools\RuntimeCheck')
$short = ([version]$Version).ToString(2)
foreach ($file in @("verification-v$Version.json","offline-v$short.json","visual-v$short.json","community-v$short.json","runtime-v$short.json")) { Copy-Item -LiteralPath (Join-Path $root ('checks\' + $file)) -Destination (Join-Path $stage 'checks') }
foreach ($file in @('recovery-v0.4.json','runtime-v0.4.json','heartbeat-v0.4.json')) { if (Test-Path -LiteralPath (Join-Path $root ('checks\' + $file))) { Copy-Item -LiteralPath (Join-Path $root ('checks\' + $file)) -Destination (Join-Path $stage 'checks') } }
$community = Join-Path $root 'Community'
foreach ($relative in @(& git -C $community ls-files)) {
    $source = Join-Path $community $relative
    $target = Join-Path (Join-Path $stage 'Community') $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
$zip = Join-Path $root ('Harbor-v' + $Version + '.zip')
# Some template files carry Unix-epoch timestamps, outside ZIP's supported range.
# Normalize the staged copies only.
Get-ChildItem -LiteralPath $stage -File -Force -Recurse | Where-Object { $_.LastWriteTime.Year -lt 1980 } | ForEach-Object { $_.LastWriteTime = [datetime]'1980-01-02' }
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
Write-Output $zip
