param(
    [ValidateSet('snapshot','capture','surfaces','uia','command')][string]$Action = 'snapshot',
    [string]$OutFile,
    [string]$Command = 'status'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if (-not ('HarborCheck.Native' -as [type])) {
    Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
namespace HarborCheck {
 public static class Native {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] public struct AppBarData { public int Size; public IntPtr Window; public uint Callback,Edge; public Rect Rect; public IntPtr Param; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder b,int max);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder b,int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
  [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint m,ref AppBarData d);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 }
}
'@
}
function Send-HarborCommand([string]$Text) {
    $client = New-Object IO.Pipes.NamedPipeClientStream('.',("Harbor.Desktop." + $env:USERNAME),[IO.Pipes.PipeDirection]::InOut)
    $client.Connect(3000)
    $writer = New-Object IO.StreamWriter($client)
    $writer.AutoFlush = $true
    $reader = New-Object IO.StreamReader($client)
    $writer.WriteLine($Text)
    $readTask = $reader.ReadToEndAsync()
    if (-not $readTask.Wait(5000)) { $client.Dispose(); throw 'Harbor did not respond within 5 seconds.' }
    $response = $readTask.Result
    $reader.Dispose(); $client.Dispose()
    return $response
}
if ($Action -eq 'command') {
    $result = Send-HarborCommand $Command
    if ($OutFile) { [IO.File]::WriteAllText($OutFile,$result) } else { $result }
    return
}
if ($Action -eq 'capture') {
    $screens = [System.Windows.Forms.Screen]::AllScreens
    foreach ($screen in $screens) {
        $bounds = $screen.Bounds
        $bitmap = New-Object Drawing.Bitmap($bounds.Width,$bounds.Height)
        $g = [Drawing.Graphics]::FromImage($bitmap)
        $g.CopyFromScreen($bounds.Location,[Drawing.Point]::Empty,$bounds.Size)
        $suffix = if ($screen.Primary) { 'primary' } else { 'secondary' }
        $target = $OutFile -replace '\.png$',("-"+$suffix+".png")
        $bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bitmap.Dispose()
        $target
    }
    return
}
if ($Action -eq 'surfaces') {
    $status = Send-HarborCommand 'status' | ConvertFrom-Json
    $index = 0
    foreach ($surface in $status.Surfaces | Where-Object IsVisible) {
        $bounds = $surface.Bounds
        $width = $bounds.Right - $bounds.Left
        $height = $bounds.Bottom - $bounds.Top
        $bitmap = New-Object Drawing.Bitmap($width,$height)
        $g = [Drawing.Graphics]::FromImage($bitmap)
        $g.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$bitmap.Size)
        $target = $OutFile -replace '\.png$',("-"+$index+".png")
        $bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bitmap.Dispose()
        $target
        $index++
    }
    return
}
$windows = New-Object 'System.Collections.Generic.List[object]'
[HarborCheck.Native]::EnumWindows({
    param($h,$p)
    $class = New-Object Text.StringBuilder 256
    [void][HarborCheck.Native]::GetClassName($h,$class,256)
    $text = New-Object Text.StringBuilder 1024
    [void][HarborCheck.Native]::GetWindowText($h,$text,1024)
    [uint32]$owner = 0
    [void][HarborCheck.Native]::GetWindowThreadProcessId($h,[ref]$owner)
    $visible = [HarborCheck.Native]::IsWindowVisible($h)
    if (($visible -and $text.Length -gt 0) -or $class.ToString() -match 'Shell_.*TrayWnd') {
        $rect = New-Object HarborCheck.Native+Rect
        [void][HarborCheck.Native]::GetWindowRect($h,[ref]$rect)
        $windows.Add([pscustomobject]@{Handle=$h.ToInt64();Pid=$owner;Title=$text.ToString();Class=$class.ToString();Visible=$visible;Bounds=$rect})
    }
    return $true
},[IntPtr]::Zero) | Out-Null
if ($Action -eq 'uia') {
    $elements = @()
    foreach ($window in @($windows | Where-Object { $_.Title -like 'Harbor*' })) {
        $element = [Windows.Automation.AutomationElement]::FromHandle([IntPtr]$window.Handle)
        $descendants = $element.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
        foreach ($child in $descendants) {
            $current = $child.Current
            if ($current.Name -or $current.ControlType.ProgrammaticName -match 'Edit|List|Combo') {
                $elements += [pscustomobject]@{Window=$window.Title;Name=$current.Name;Type=$current.ControlType.ProgrammaticName;Bounds=$current.BoundingRectangle;Enabled=$current.IsEnabled;Offscreen=$current.IsOffscreen}
            }
        }
    }
    $result = $elements | ConvertTo-Json -Depth 5
} else {
    $data = New-Object HarborCheck.Native+AppBarData
    $data.Size = [Runtime.InteropServices.Marshal]::SizeOf($data)
    $state = [HarborCheck.Native]::SHAppBarMessage(4,[ref]$data).ToUInt64()
    $result = [pscustomobject]@{
        Time=[DateTimeOffset]::Now.ToString('O')
        TaskbarState=$state
        Windows=$windows.ToArray()
        Screens=@([System.Windows.Forms.Screen]::AllScreens | Select-Object DeviceName,Primary,Bounds,WorkingArea)
        Foreground=[HarborCheck.Native]::GetForegroundWindow().ToInt64()
    } | ConvertTo-Json -Depth 6
}
if ($OutFile) { [IO.File]::WriteAllText($OutFile,$result) } else { $result }
