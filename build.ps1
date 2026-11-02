param([string]$Sdk = '', [switch]$Install, [switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
if (-not $Sdk) {
    $known = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'FastDrop\.dotnet\dotnet.exe'
    $Sdk = if (Test-Path -LiteralPath $known) { $known } else { (Get-Command dotnet).Source }
}
$publish = Join-Path $PSScriptRoot ('Build\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
& $Sdk publish (Join-Path $PSScriptRoot 'src\Harbor.csproj') -c Release --self-contained false -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'Harbor build failed.' }
& $Sdk publish (Join-Path $PSScriptRoot 'SessionSource\HarborSession.csproj') -c Release --self-contained false -o (Join-Path $publish 'Session') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Harbor sign-in supervisor build failed.' }
$exe = Join-Path $PSScriptRoot 'App\Harbor.exe'
if ($BuildOnly) { Write-Output "Build ready: $publish"; return }
$sessionExe = Join-Path $PSScriptRoot 'Session\HarborSession.exe'
if (Test-Path -LiteralPath $sessionExe) {
    Start-Process -FilePath $sessionExe -ArgumentList '--stop' -WindowStyle Hidden -Wait
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $hostProcesses = @(Get-CimInstance Win32_Process -Filter "Name='HarborSession.exe'" | Where-Object { $_.ExecutablePath -eq $sessionExe })
        if ($hostProcesses.Count -eq 0) { break }
        Start-Sleep -Milliseconds 200
    }
    if ($hostProcesses.Count -gt 0) { throw 'The session supervisor is still closing. The running installation has not been changed.' }
}
$running = @(Get-CimInstance Win32_Process -Filter "Name='Harbor.exe'" | Where-Object { $_.ExecutablePath -eq $exe })
$restart = @($running | Where-Object { $_.CommandLine -notmatch '--(watchdog|status|quit|verify|render-check|probe-shell|restore)' }).Count -gt 0
if ($running.Count -gt 0) {
    Start-Process -FilePath $exe -ArgumentList '--quit' -WindowStyle Hidden -Wait
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $remaining = @(Get-CimInstance Win32_Process -Filter "Name='Harbor.exe'" | Where-Object { $_.ExecutablePath -eq $exe })
        if ($remaining.Count -eq 0) { break }
        Start-Sleep -Milliseconds 200
    }
    if ($remaining.Count -gt 0) { throw "Harbor is still closing. The new build is safe in $publish." }
}
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'App') -Force | Out-Null
Get-ChildItem -LiteralPath $publish -File | Copy-Item -Destination (Join-Path $PSScriptRoot 'App') -Force
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Session') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $publish 'Session') -File | Copy-Item -Destination (Join-Path $PSScriptRoot 'Session') -Force
if ($restart) { Start-Process -FilePath $exe -WindowStyle Hidden }
if ($Install) {
    $desktop = [Environment]::GetFolderPath('DesktopDirectory')
    $start = Join-Path ([Environment]::GetFolderPath('Programs')) 'Harbor'
    New-Item -ItemType Directory -Path $start -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in @($desktop,$start)) {
        $link = $shell.CreateShortcut((Join-Path $folder 'Harbor.lnk'))
        $link.TargetPath = $exe; $link.WorkingDirectory = Split-Path -Parent $exe
        $link.IconLocation = "$exe,0"; $link.Description = 'Harbor desktop'; $link.Save()
    }
    $restore = $shell.CreateShortcut((Join-Path $start 'Restore Windows.lnk'))
    $restore.TargetPath = $exe; $restore.Arguments = '--restore'; $restore.IconLocation = "$env:WINDIR\System32\shell32.dll,167"
    $restore.Description = 'Exit Harbor and restore the Windows desktop'; $restore.Save()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    Write-Output "Installed Harbor shortcuts. Run: $exe"
}
