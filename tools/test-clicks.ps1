$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'desktop.ps1') -Action command -Command status -OutFile (Join-Path $project 'checks\click-test-start.json')
& (Join-Path $PSScriptRoot 'input-probe.ps1') -OutFile (Join-Path $project 'checks\click-test-input.json') | Out-Null
$initial = Get-Content -Raw -LiteralPath (Join-Path $project 'checks\click-test-start.json') | ConvertFrom-Json
$dash = [char]0x2014
$results = New-Object 'System.Collections.Generic.List[object]'
function Check([string]$Name,[bool]$Passed) { $results.Add([pscustomobject]@{Name=$Name;Passed=$Passed}); if (-not $Passed) { throw "Failed: $Name" } }
function Window([string]$Name) {
    $condition=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$Name)
    [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,$condition)
}
function Child($Parent,[string]$Name) {
    $condition=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$Name)
    $Parent.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
}
function Foreground-Process {
    [uint32]$owner=0
    [void][HarborCheck.Native]::GetWindowThreadProcessId([HarborCheck.Native]::GetForegroundWindow(),[ref]$owner)
    (Get-Process -Id $owner).Name
}
function Click($Element,[string]$Name) {
    if (-not $Element) { throw "Missing control: $Name" }
    $r=$Element.Current.BoundingRectangle
    if ($r.IsEmpty -or $Element.Current.IsOffscreen) { throw "Control is not visible: $Name" }
    $x=[int]($r.Left+$r.Width/2); $y=[int]($r.Top+$r.Height/2)
    [void][HarborCheck.Native]::SetCursorPos($x,$y)
    $point=New-Object HarborCheck.Native+Point
    [void][HarborCheck.Native]::GetCursorPos([ref]$point)
    Check ($Name+': cursor reaches the control') ($point.X -eq $x -and $point.Y -eq $y)
    $probePoint=New-Object HarborInputProbe+Point; $probePoint.X=$point.X; $probePoint.Y=$point.Y
    $hit=[HarborInputProbe]::WindowFromPoint($probePoint)
    [uint32]$owner=0; [void][HarborCheck.Native]::GetWindowThreadProcessId($hit,[ref]$owner)
    Check ($Name+': pointer is over Harbor before clicking') ($owner -eq $initial.Pid)
    try {
        [HarborCheck.Native]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        Check ($Name+': Harbor owns focus on mouse-down') ((Foreground-Process) -eq 'Harbor')
    }
    finally { [HarborCheck.Native]::mouse_event(4,0,0,0,[UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 300
}
function Dock-Button([string]$Prefix) {
    for($attempt=0;$attempt -lt 6;$attempt++) {
    $current=Send-HarborCommand 'status' | ConvertFrom-Json
    foreach($dock in $current.Surfaces | Where-Object { $_.Title.EndsWith('dock') -and $_.IsVisible }) {
        $element=[Windows.Automation.AutomationElement]::FromHandle([IntPtr]$dock.Handle)
        $buttons=$element.FindAll([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button)))
        foreach($button in $buttons) { if($button.Current.Name.StartsWith($Prefix)) { return $button } }
    }
    Start-Sleep -Milliseconds 120
    }
    throw "No visible dock button: $Prefix"
}
function Ctrl-Esc {
    try {
        [HarborCheck.Native]::keybd_event(0xA2,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 40
        [HarborCheck.Native]::keybd_event(0x1B,0,0,[UIntPtr]::Zero)
        [HarborCheck.Native]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
    }
    finally { [HarborCheck.Native]::keybd_event(0xA2,0,2,[UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 400
}
try {
    if ((Foreground-Process) -ne 'RocketLeague') {
        [void](Send-HarborCommand 'settings'); Start-Sleep -Milliseconds 300
        Click (Dock-Button 'Rocket League') 'Switch to Rocket League'
        $settings=Window 'Harbor settings'
        if($settings) { [void][HarborCheck.Native]::PostMessage([IntPtr]$settings.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
        Start-Sleep -Milliseconds 300
    }
    Check 'Test starts with Rocket League owning focus' ((Foreground-Process) -eq 'RocketLeague')
    Ctrl-Esc
    $menu=Window "Harbor $dash apps"
    Check 'Ctrl + Esc transfers actual foreground focus from the game' ($null -ne $menu -and (Foreground-Process) -eq 'Harbor')
    $clip=New-Object HarborInputProbe+Rect; [void][HarborInputProbe]::GetClipCursor([ref]$clip)
    Check 'The game releases its primary-monitor cursor confinement' ($clip.Right -gt 1920)
    $oldCalculator=Window 'Calculator'
    (Child $menu 'Search apps').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('Calculator')
    Start-Sleep -Milliseconds 400
    Click (Child $menu 'Open Calculator') 'Menu app single-click'
    Start-Sleep -Seconds 2
    $calculator=Window 'Calculator'
    Check 'One menu click launches and focuses Calculator' ($null -ne $calculator -and [HarborCheck.Native]::GetForegroundWindow() -eq [IntPtr]$calculator.Current.NativeWindowHandle)
    Click (Dock-Button 'File Explorer') 'Dock app single-click'
    Start-Sleep -Seconds 1
    $explorerHandle=[HarborCheck.Native]::GetForegroundWindow()
    $class=New-Object Text.StringBuilder 256
    [void][HarborCheck.Native]::GetClassName($explorerHandle,$class,256)
    Check 'One dock click opens and focuses File Explorer' ((Foreground-Process) -eq 'explorer' -and $class.ToString() -eq 'CabinetWClass')
    Click (Dock-Button 'File Explorer') 'Minimize active dock app'
    Check 'Clicking the active dock app still minimizes it' ([HarborCheck.Native]::IsIconic($explorerHandle))
    Click (Dock-Button 'File Explorer') 'Restore dock app'
    Check 'One click restores and focuses the minimized app' (-not [HarborCheck.Native]::IsIconic($explorerHandle) -and [HarborCheck.Native]::GetForegroundWindow() -eq $explorerHandle)
    Click (Dock-Button 'Open apps') 'Dock launcher single-click'
    $menu=Window "Harbor $dash apps"
    Check 'One dock launcher click opens the menu with focus' ($null -ne $menu -and (Foreground-Process) -eq 'Harbor')
    (Child $menu 'Close apps menu').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    if(-not $oldCalculator -and $calculator) { [void][HarborCheck.Native]::PostMessage([IntPtr]$calculator.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
    Click (Dock-Button 'Rocket League') 'Return to Rocket League'
    Check 'One dock click returns foreground focus to Rocket League' ((Foreground-Process) -eq 'RocketLeague')
}
finally {
    $results.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'checks\test-clicks.json')
    $results.ToArray() | Format-Table -AutoSize
}
