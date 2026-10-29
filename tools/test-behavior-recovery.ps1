param(
    [Parameter(Mandatory=$true)][string]$Build,
    [Parameter(Mandatory=$true)][string]$VerificationProfile
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'App\Harbor.exe'
$settingsFile = Join-Path $env:LOCALAPPDATA 'Harbor\settings.json'
$modStore = Join-Path $env:LOCALAPPDATA 'Harbor\Mods'
$checkDir = Join-Path $root ('checks\v0.4-recovery-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $checkDir | Out-Null
$backupFile = Join-Path $checkDir 'original-settings.json'
$fixtureExe = Join-Path $PSScriptRoot 'RuntimeCheck\bin\Release\net9.0-windows\win-x64\HarborTestDesktop.exe'
if (-not (Test-Path -LiteralPath (Join-Path $Build 'Harbor.exe')) -or -not (Test-Path -LiteralPath $fixtureExe)) { throw 'Build Harbor and the RuntimeCheck fixture first.' }
$testIds = @('test.harbor-recovery.window-topmost','test.harbor-recovery.window-opacity','test.harbor-recovery.window-placement')
foreach ($id in $testIds) { if (Test-Path -LiteralPath (Join-Path $modStore $id)) { throw "A test directory already exists: $id" } }
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HarborRecoveryProbe {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr Style(IntPtr h,int index);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr h,out uint color,out byte alpha,out uint flags);
}
'@
function Status { return (& (Join-Path $PSScriptRoot 'desktop.ps1') -Action command -Command status | ConvertFrom-Json) }
function WaitClosed {
    for ($i=0; $i -lt 40; $i++) {
        $left = @(Get-CimInstance Win32_Process -Filter "Name='Harbor.exe'" | Where-Object { $_.ExecutablePath -eq $exe })
        if ($left.Count -eq 0) { return }; Start-Sleep -Milliseconds 200
    }
    throw 'Harbor did not finish closing.'
}
function Quit {
    Start-Process -FilePath $exe -ArgumentList '--quit' -WindowStyle Hidden -Wait
    WaitClosed
}
function Probe([string]$name) {
    $path=Join-Path $checkDir ($name+'.json')
    Start-Process -FilePath $exe -ArgumentList ('--probe-shell "'+$path+'"') -WindowStyle Hidden -Wait
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
}
function Areas($probe) { return (($probe.Monitors | Sort-Object Device | ForEach-Object { $_.Device+':'+$_.Work.Left+','+$_.Work.Top+','+$_.Work.Right+','+$_.Work.Bottom }) -join '|') }
function EffectState {
    $style=[HarborRecoveryProbe]::Style($script:fixtureHandle,-20).ToInt64()
    [uint32]$color=0; [byte]$alpha=255; [uint32]$flags=0
    $layered=[HarborRecoveryProbe]::GetLayeredWindowAttributes($script:fixtureHandle,[ref]$color,[ref]$alpha,[ref]$flags)
    return [pscustomobject]@{Topmost=($style-band 8)-ne 0;Layered=($style-band 0x80000)-ne 0;Alpha=$alpha;Readable=$layered}
}
function WaitEffects {
    for ($i=0; $i -lt 60; $i++) { $state=EffectState; if ($state.Topmost -and $state.Layered -and $state.Readable -and $state.Alpha -eq 179) { return $state }; Start-Sleep -Milliseconds 200 }
    Status|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $checkDir 'effects-failure-status.json') -Encoding UTF8
    throw ('The controlled fixture did not receive both effects: '+($state|ConvertTo-Json -Compress))
}
function EffectsRestored {
    for ($i=0; $i -lt 20; $i++) { $state=EffectState; if ($state.Topmost -eq $script:original.Topmost -and $state.Layered -eq $script:original.Layered) { return $true }; Start-Sleep -Milliseconds 100 }
    return $false
}
$initial=Status
$startup=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').Harbor
$fixture=$null; $settingsBackedUp=$false; $created=@(); $result=[ordered]@{}
try {
    Quit
    Copy-Item -LiteralPath $settingsFile -Destination $backupFile; $settingsBackedUp=$true
    $config=Get-Content -LiteralPath $backupFile -Raw | ConvertFrom-Json
    $baseline=Probe 'baseline'
    Get-ChildItem -LiteralPath $Build -File | Copy-Item -Destination (Join-Path $root 'App') -Force
    foreach ($component in @('window-topmost','window-opacity','window-placement')) {
        $definitionPath=Join-Path $VerificationProfile ('Mods\harbor.'+$component+'\manifest.json')
        $mod=Get-Content -LiteralPath $definitionPath -Raw | ConvertFrom-Json
        $mod.Id='test.harbor-recovery.'+$component; $mod.Name='Harbor controlled recovery check — '+$component
        $folder=Join-Path $modStore $mod.Id; New-Item -ItemType Directory -Path $folder | Out-Null; $created += $folder
        [IO.File]::WriteAllText((Join-Path $folder 'manifest.json'),($mod|ConvertTo-Json -Depth 30),(New-Object Text.UTF8Encoding($false)))
        $values=[pscustomobject]@{apps='HarborTestDesktop.exe';titles='Harbor non-game focus check';exclude='';opacity='70';monitor='primary';region='left';gap='12'}
        $config.ModOptions | Add-Member -MemberType NoteProperty -Name $mod.Id -Value $values -Force
        $config.EnabledMods += $mod.Id
    }
    [IO.File]::WriteAllText($settingsFile,($config|ConvertTo-Json -Depth 40),(New-Object Text.UTF8Encoding($false)))
    $handleFile=Join-Path $checkDir 'fixture-handle.txt'
    $fixture=Start-Process -FilePath $fixtureExe -ArgumentList ('--native-neutral "'+$handleFile+'" 90') -WindowStyle Hidden -PassThru
    for ($i=0; $i -lt 40 -and -not (Test-Path -LiteralPath $handleFile); $i++) { Start-Sleep -Milliseconds 100 }
    $script:fixtureHandle=[IntPtr][long](Get-Content -LiteralPath $handleFile -Raw)
    $script:original=EffectState
    Start-Process -FilePath $exe -WindowStyle Hidden
    $live=WaitEffects; $state=Status
    $result.LiveTopmostApplied=$live.Topmost; $result.LiveOpacityApplied=$live.Alpha -eq 179
    $rect=New-Object HarborRecoveryProbe+Rect; [void][HarborRecoveryProbe]::GetWindowRect($script:fixtureHandle,[ref]$rect)
    $primaryName=[System.Windows.Forms.Screen]::PrimaryScreen.DeviceName
    $primary=$state.Surfaces | Where-Object { $_.Monitor -eq $primaryName } | Select-Object -First 1
    $work=$primary.WorkArea
    $result.PlacementFitsWorkArea=$rect.Left -ge $work.Left -and $rect.Top -ge $work.Top -and $rect.Right -le $work.Right -and $rect.Bottom -le $work.Bottom -and ($rect.Right-$rect.Left) -lt (($work.Right-$work.Left)*0.7)
    Quit; $normal=Probe 'normal-exit'
    $result.NormalEffectsRestored=EffectsRestored
    EffectState|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $checkDir 'normal-window-effects.json') -Encoding UTF8
    $result.NormalTaskbarsVisible=@($normal.Taskbars|Where-Object {-not $_.Visible}).Count -eq 0
    $result.NormalIconsVisible=@($normal.Icons|Where-Object {-not $_.Visible}).Count -eq 0
    $result.NormalAreasMatchBaseline=(Areas $normal) -eq (Areas $baseline)
    Start-Process -FilePath $exe -WindowStyle Hidden
    $null=WaitEffects; $state=Status
    $owned=Get-CimInstance Win32_Process -Filter ('ProcessId='+$state.Pid)
    if ($owned.ExecutablePath -ne $exe -or $state.Version -ne '0.4.0') { throw 'Unexpected process; crash check aborted.' }
    Stop-Process -Id $state.Pid -Force; WaitClosed
    $crash=Probe 'crash-exit'
    $result.CrashEffectsRestored=EffectsRestored
    EffectState|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $checkDir 'crash-window-effects.json') -Encoding UTF8
    $result.CrashTaskbarsVisible=@($crash.Taskbars|Where-Object {-not $_.Visible}).Count -eq 0
    $result.CrashIconsVisible=@($crash.Icons|Where-Object {-not $_.Visible}).Count -eq 0
    $result.CrashAreasMatchBaseline=(Areas $crash) -eq (Areas $baseline)
}
finally {
    Quit
    if ($settingsBackedUp) { Copy-Item -LiteralPath $backupFile -Destination $settingsFile -Force }
    foreach ($folder in $created) { $manifest=Join-Path $folder 'manifest.json'; if (Test-Path -LiteralPath $manifest) { Remove-Item -LiteralPath $manifest }; if (@(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) { Remove-Item -LiteralPath $folder } }
    if ($fixture) { if (-not $fixture.HasExited) { [void]$fixture.CloseMainWindow(); if (-not $fixture.WaitForExit(1500)) { $fixture.Kill() } }; $fixture.Dispose() }
    Start-Process -FilePath $exe -WindowStyle Hidden
}
Start-Sleep -Seconds 4
$after=Status
$result.FinalPinsPreserved=$after.PinCount -eq $initial.PinCount
$result.EnabledModsPreserved=(@($after.EnabledMods|Sort-Object)-join '|') -eq (@($initial.EnabledMods|Sort-Object)-join '|')
$result.StartupPreserved=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').Harbor -eq $startup
$result.TemporaryModsRemoved=@($testIds|Where-Object {Test-Path -LiteralPath (Join-Path $modStore $_)}).Count -eq 0
$result.FinalDesktopsHealthy=@($after.Desktops|Where-Object {-not $_.Healthy}).Count -eq 0
$result.FinalNativeTaskbarsHidden=@($after.Taskbars|Where-Object Visible).Count -eq 0
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'checks\recovery-v0.4.json') -Encoding UTF8
$result|ConvertTo-Json
if (@($result.Values|Where-Object {$_ -ne $true}).Count) { throw 'A behavior recovery check failed. See recovery-v0.4.json.' }
