param([string]$OutFile = '')
$ErrorActionPreference = 'Stop'
$probeOutputPath = $OutFile
. (Join-Path $PSScriptRoot 'desktop.ps1') -Action command -Command status -OutFile (Join-Path (Split-Path -Parent $PSScriptRoot) 'checks\input-status.json')
if (-not ('HarborInputProbe' -as [type])) {
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HarborInputProbe {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct GuiInfo { public int Size; public uint Flags; public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret; public Rect CaretRect; }
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")] public static extern bool GetClipCursor(out Rect rect);
 [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
}
'@
}
$status = Get-Content -Raw -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'checks\input-status.json') | ConvertFrom-Json
$clip = New-Object HarborInputProbe+Rect
[void][HarborInputProbe]::GetClipCursor([ref]$clip)
$gui = New-Object HarborInputProbe+GuiInfo
$gui.Size = [Runtime.InteropServices.Marshal]::SizeOf($gui)
[void][HarborInputProbe]::GetGUIThreadInfo(0,[ref]$gui)
$cursor = New-Object HarborCheck.Native+Point
[void][HarborCheck.Native]::GetCursorPos([ref]$cursor)
[uint32]$owner = 0
[void][HarborCheck.Native]::GetWindowThreadProcessId([HarborCheck.Native]::GetForegroundWindow(),[ref]$owner)
$surfaces = foreach ($surface in $status.Surfaces) {
    $extended = [HarborInputProbe]::GetWindowLong([IntPtr]$surface.Handle,-20).ToInt64()
    [pscustomobject]@{Title=$surface.Title;Monitor=$surface.Monitor;NoActivate=(($extended -band 0x08000000) -ne 0);Visible=$surface.IsVisible}
}
$report = [pscustomobject]@{ForegroundProcess=(Get-Process -Id $owner).Name;Active=$gui.Active.ToInt64();Focus=$gui.Focus.ToInt64();Capture=$gui.Capture.ToInt64();Cursor=$cursor;Clip=$clip;Surfaces=$surfaces}
$json = $report | ConvertTo-Json -Depth 6
if ($probeOutputPath) { [IO.File]::WriteAllText($probeOutputPath,$json) }
$json
