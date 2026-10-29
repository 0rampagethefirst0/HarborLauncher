param([ValidateSet('menus','keys','ctrl-esc','recovery','hang')][string]$Part = 'menus')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'desktop.ps1') -Action command -Command status -OutFile (Join-Path $project 'checks\test-current.json')
$status = Get-Content -Raw -LiteralPath (Join-Path $project 'checks\test-current.json') | ConvertFrom-Json
$harborPid = $status.Pid
$dash = [char]0x2014
$report = New-Object 'System.Collections.Generic.List[object]'
function Check([string]$Name,[bool]$Passed,[string]$Detail='') {
    $report.Add([pscustomobject]@{Name=$Name;Passed=$Passed;Detail=$Detail})
    if (-not $Passed) { throw "Failed: $Name $Detail" }
}
function Window([string]$Name) {
    $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$Name)
    return [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,$condition)
}
function Child($Window,[string]$Name) {
    $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$Name)
    return $Window.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
}
function Invoke-Element($Element) { $Element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Click-Element($Element) {
    $bounds = $Element.Current.BoundingRectangle
    if ($bounds.IsEmpty -or $Element.Current.IsOffscreen) { throw 'The intended control is not visible.' }
    [void][HarborCheck.Native]::SetCursorPos([int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2))
    [HarborCheck.Native]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    [HarborCheck.Native]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
}
function Key([byte]$Code,[bool]$Up=$false) { [HarborCheck.Native]::keybd_event($Code,0,$(if($Up){2}else{0}),[UIntPtr]::Zero) }
try {
    if ($Part -eq 'menus') {
        $launcher = Window "Harbor $dash apps"
        if (-not $launcher) { [void](Send-HarborCommand 'launcher'); Start-Sleep -Milliseconds 500; $launcher = Window "Harbor $dash apps" }
        Check 'Launcher opens' ($null -ne $launcher)
        $search = Child $launcher 'Search apps'
        $search.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('Calculator')
        Start-Sleep -Milliseconds 600
        $result = Child $launcher 'Open Calculator'
        Check 'Search finds Calculator' ($null -ne $result)
        $existing = Window 'Calculator'
        Invoke-Element $result
        Start-Sleep -Seconds 3
        $calculator = Window 'Calculator'
        Check 'Search result launches a real app' ($null -ne $calculator)
        if (-not $existing) { [void][HarborCheck.Native]::PostMessage([IntPtr]$calculator.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
        foreach ($command in @('quick','calendar','arrange','settings')) {
            [void](Send-HarborCommand $command)
            Start-Sleep -Milliseconds 500
            $title = switch($command) {'quick' {"Harbor $dash quick settings"} 'calendar' {"Harbor $dash calendar"} 'arrange' {"Harbor $dash arrange windows"} 'settings' {'Harbor settings'}}
            $panel = Window $title
            Check ("Open "+$command) ($null -ne $panel)
            if ($panel) { [void][HarborCheck.Native]::PostMessage([IntPtr]$panel.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
            Start-Sleep -Milliseconds 150
        }
    }
    elseif ($Part -eq 'keys') {
        [void](Send-HarborCommand 'settings'); Start-Sleep -Milliseconds 400
        $settings = Window 'Harbor settings'
        (Child $settings 'Use a 24-hour clock').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
        (Child $settings 'Use a 24-hour clock').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
        [void][HarborCheck.Native]::PostMessage([IntPtr]$settings.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)
        Start-Sleep -Milliseconds 300
        Key 0x5B; Start-Sleep -Milliseconds 60; Key 0x5B $true
        Start-Sleep -Milliseconds 700
        $launcher = Window "Harbor $dash apps"
        Check 'Windows key opens Harbor' ($null -ne $launcher)
        Invoke-Element (Child $launcher 'Close apps menu')
        Start-Sleep -Milliseconds 300
        Key 0x5B; Start-Sleep -Milliseconds 40; Key 0x52; Key 0x52 $true; Key 0x5B $true
        Start-Sleep -Milliseconds 900
        $run = Window 'Run'
        Check 'Win + R still opens Run' ($null -ne $run)
        if ($run) { [void][HarborCheck.Native]::PostMessage([IntPtr]$run.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
    }
    elseif ($Part -eq 'ctrl-esc') {
        function Native-StartWindows {
            $owners = @(Get-Process -Name StartMenuExperienceHost -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
            $found = New-Object 'System.Collections.Generic.List[object]'
            [void][HarborCheck.Native]::EnumWindows({
                param($h,$p)
                [uint32]$owner = 0
                [void][HarborCheck.Native]::GetWindowThreadProcessId($h,[ref]$owner)
                if ($owners -contains $owner -and [HarborCheck.Native]::IsWindowVisible($h)) {
                    [int]$cloaked = 0
                    [void][HarborCheck.Native]::DwmGetWindowAttribute($h,14,[ref]$cloaked,4)
                    $bounds = New-Object HarborCheck.Native+Rect
                    [void][HarborCheck.Native]::GetWindowRect($h,[ref]$bounds)
                    if ($cloaked -eq 0 -and ($bounds.Right-$bounds.Left) -gt 100 -and ($bounds.Bottom-$bounds.Top) -gt 100) { $found.Add($h.ToInt64()) }
                }
                return $true
            },[IntPtr]::Zero)
            return $found.ToArray()
        }
        function Ctrl-Escape([byte]$Ctrl=0xA2,[int]$Repeats=1,[bool]$CtrlFirst=$false) {
            try {
                Key $Ctrl; Start-Sleep -Milliseconds 45
                for ($i=0;$i -lt $Repeats;$i++) { Key 0x1B; Start-Sleep -Milliseconds 15 }
                if ($CtrlFirst) { Key $Ctrl $true }
                Key 0x1B $true
                Key $Ctrl $true
            }
            finally { Key 0x1B $true; Key $Ctrl $true }
            Start-Sleep -Milliseconds 650
        }
        function Check-StartClosed([string]$Name) { Check $Name (@(Native-StartWindows).Count -eq 0) }
        $launcher = Window "Harbor $dash apps"
        if ($launcher) { Invoke-Element (Child $launcher 'Close apps menu'); Start-Sleep -Milliseconds 200 }
        Check-StartClosed 'Windows Start is closed before shortcut test'
        Ctrl-Escape 0xA2
        $launcher = Window "Harbor $dash apps"
        Check 'Left Ctrl + Esc opens Harbor' ($null -ne $launcher)
        Check-StartClosed 'Ctrl + Esc leaves Windows Start closed'
        Ctrl-Escape 0xA2
        Check 'Second Ctrl + Esc closes Harbor menu' ($null -eq (Window "Harbor $dash apps"))
        Ctrl-Escape 0xA3
        $launcher = Window "Harbor $dash apps"
        Check 'Right Ctrl + Esc opens Harbor' ($null -ne $launcher)
        Check-StartClosed 'Right Ctrl shortcut leaves Windows Start closed'
        Invoke-Element (Child $launcher 'Close apps menu')
        Ctrl-Escape 0xA2 4
        $launcher = Window "Harbor $dash apps"
        Check 'Held Escape repeats open the menu once' ($null -ne $launcher)
        Invoke-Element (Child $launcher 'Close apps menu')
        Ctrl-Escape 0xA2 1 $true
        $launcher = Window "Harbor $dash apps"
        Check 'Releasing Ctrl before Escape still opens Harbor' ($null -ne $launcher)
        Check-StartClosed 'Alternate release order leaves Windows Start closed'
        Check 'Ctrl, Escape, and Windows keys are released' (@(0x11,0x1B,0x5B,0x5C | Where-Object { ([HarborCheck.Native]::GetAsyncKeyState($_) -band 0x8000) -ne 0 }).Count -eq 0)
        Invoke-Element (Child $launcher 'Close apps menu')
        $existingManagers = @(Get-Process -Name Taskmgr -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
        try {
            Key 0xA2; Key 0xA0; Start-Sleep -Milliseconds 45
            Key 0x1B; Key 0x1B $true; Key 0xA0 $true; Key 0xA2 $true
            Start-Sleep -Seconds 2
            $manager = Window 'Task Manager'
            Check 'Ctrl + Shift + Esc still opens Task Manager' ($null -ne $manager)
            Check 'Ctrl + Shift + Esc does not open Harbor menu' ($null -eq (Window "Harbor $dash apps"))
            if ($manager -and $existingManagers -notcontains $manager.Current.ProcessId) { [void][HarborCheck.Native]::PostMessage([IntPtr]$manager.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
        }
        finally { Key 0x1B $true; Key 0xA0 $true; Key 0xA2 $true }
    }
    elseif ($Part -eq 'recovery') {
        $baseline = Get-Content -Raw -LiteralPath (Join-Path $project 'checks\before.json') | ConvertFrom-Json
        Check 'Recovery starts with native taskbars hidden' (@($status.Taskbars | Where-Object Visible).Count -eq 0)
        Stop-Process -Id $harborPid -Force
        Start-Sleep -Seconds 3
        & (Join-Path $PSScriptRoot 'desktop.ps1') -Action snapshot -OutFile (Join-Path $project 'checks\after-crash.json')
        $after = Get-Content -Raw -LiteralPath (Join-Path $project 'checks\after-crash.json') | ConvertFrom-Json
        $taskbars = @($after.Windows | Where-Object { $_.Class -in @('Shell_TrayWnd','Shell_SecondaryTrayWnd') })
        Check 'Forced crash restores both Windows taskbars' ($taskbars.Count -eq 2 -and @($taskbars | Where-Object { -not $_.Visible }).Count -eq 0)
        Check 'Original taskbar state restored' ($after.TaskbarState -eq $baseline.TaskbarState)
        Check 'No Harbor UI remains after crash' (-not (Get-Process -Id $harborPid -ErrorAction SilentlyContinue))
    }
    elseif ($Part -eq 'hang') {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HarborHangCheck {
 [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
 [DllImport("ntdll.dll")] public static extern int NtSuspendProcess(IntPtr process);
}
'@
        $handle = [HarborHangCheck]::OpenProcess(0x800,$false,$harborPid)
        if ($handle -eq [IntPtr]::Zero) { throw 'Unable to open the Harbor test process.' }
        try { Check 'Simulate an unresponsive Harbor process' ([HarborHangCheck]::NtSuspendProcess($handle) -eq 0) } finally { [void][HarborHangCheck]::CloseHandle($handle) }
        Start-Sleep -Seconds 24
        & (Join-Path $PSScriptRoot 'desktop.ps1') -Action snapshot -OutFile (Join-Path $project 'checks\after-hang.json')
        $after = Get-Content -Raw -LiteralPath (Join-Path $project 'checks\after-hang.json') | ConvertFrom-Json
        $taskbars = @($after.Windows | Where-Object { $_.Class -in @('Shell_TrayWnd','Shell_SecondaryTrayWnd') })
        Check 'Unresponsive Harbor is stopped by the helper' (-not (Get-Process -Id $harborPid -ErrorAction SilentlyContinue))
        Check 'Hang recovery restores Windows taskbars' ($taskbars.Count -eq 2 -and @($taskbars | Where-Object { -not $_.Visible }).Count -eq 0)
    }
}
finally {
    $report.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project ("checks\test-"+$Part+".json"))
    $report.ToArray() | Format-Table -AutoSize
}
