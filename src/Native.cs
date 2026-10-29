using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Placement { public int Length, Flags, ShowCommand; public Point MinPosition, MaxPosition; public Rect Normal; }
    [StructLayout(LayoutKind.Sequential)] internal struct AppBarData { public int Size; public IntPtr Window; public uint Callback, Edge; public Rect Rect; public IntPtr Param; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct FileInfo { public IntPtr Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
    [StructLayout(LayoutKind.Sequential)] internal struct MemoryInfo { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [StructLayout(LayoutKind.Sequential)] internal struct FileTime { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }
    internal delegate bool EnumProc(IntPtr window, IntPtr param);
    internal delegate void WinEventProc(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint threadId, uint time);
    [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr SetParent(IntPtr window, IntPtr parent);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll")] internal static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] internal static extern IntPtr SetWindowLong(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] internal static extern bool SetWorkArea(uint action, uint param, ref Rect area, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessageTimeout(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool SendNotifyMessage(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window, int attr, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int DwmGetWindowRect(IntPtr window, int attr, out Rect value, int size);
    internal static Rect VisibleBounds(IntPtr window, Rect fallback)
    {
        if (DwmGetWindowRect(window, 9, out var frame, Marshal.SizeOf<Rect>()) != 0 || frame.Width <= 0 || frame.Height <= 0) return fallback;
        // ApplicationFrameHost includes its maximized top resize border in the
        // DWM rectangle, unlike the left/right/bottom invisible frame borders.
        if (IsZoomed(window) && Class(window) == "ApplicationFrameWindow" && frame.Top == fallback.Top) frame.Top += Math.Clamp(frame.Left - fallback.Left, 0, 32);
        return frame;
    }
    [DllImport("kernel32.dll")] internal static extern bool GlobalMemoryStatusEx(ref MemoryInfo info);
    [DllImport("kernel32.dll")] internal static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint thread, uint attachTo, bool attach);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
    [DllImport("ole32.dll")] internal static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] internal static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHCreateItemFromParsingName(string name, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellImage item);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHDefExtractIcon(string file, int index, uint flags, out IntPtr large, out IntPtr small, uint size);
    [StructLayout(LayoutKind.Sequential)] struct IconSize { public int X, Y; }
    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellImage
    {
        [PreserveSig] int GetImage(IconSize size, int flags, out IntPtr bitmap);
    }

    internal static string Title(IntPtr hwnd) { var b = new StringBuilder(1024); GetWindowText(hwnd, b, b.Capacity); return b.ToString(); }
    internal static string Class(IntPtr hwnd) { var b = new StringBuilder(256); GetClassName(hwnd, b, b.Capacity); return b.ToString(); }
    internal static MonitorInfo Monitor(IntPtr handle) { var m = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" }; GetMonitorInfo(handle, ref m); return m; }
    internal static double Scale(IntPtr monitor) { try { GetDpiForMonitor(monitor, 0, out uint x, out _); return Math.Max(1, x / 96.0); } catch { return 1; } }
    sealed class ClickState { internal IntPtr Previous; internal bool Activating; }
    static IntPtr lastExternalForeground;
    internal static void TrackForeground(IntPtr window)
    {
        if (window == IntPtr.Zero) return;
        GetWindowThreadProcessId(window, out uint pid);
        if (pid != Environment.ProcessId && Class(window) is not "Progman" and not "WorkerW" and not "Shell_TrayWnd" and not "Shell_SecondaryTrayWnd") lastExternalForeground = window;
    }
    static IntPtr BeforeActivation()
    {
        var current = GetForegroundWindow();
        return current != IntPtr.Zero ? current : IsWindow(lastExternalForeground) ? lastExternalForeground : IntPtr.Zero;
    }
    static readonly ConditionalWeakTable<Window, ClickState> Clicks = new();
    internal static IntPtr ForegroundBeforeClick(Window window)
    {
        if (!Clicks.TryGetValue(window, out var state)) return IntPtr.Zero;
        IntPtr result = state.Previous; state.Previous = IntPtr.Zero; return result;
    }
    internal static void ActivateForClick(Window window)
    {
        var state = Clicks.GetValue(window, _ => new ClickState()); state.Previous = BeforeActivation(); state.Activating = true;
        Activate(new WindowInteropHelper(window).Handle);
    }
    internal static void ToolWindow(Window window)
    {
        var h = new WindowInteropHelper(window).Handle;
        // ShowActivated=false prevents startup focus theft. It must not also disable
        // activation for a real click, which would leave fullscreen games receiving input.
        long style = (GetWindowLong(h, -20).ToInt64() | 0x80) & ~0x08000000L;
        SetWindowLong(h, -20, new IntPtr(style));
        if (HwndSource.FromHwnd(h)?.CompositionTarget is HwndTarget target) target.RenderMode = RenderMode.SoftwareOnly;
        var source = HwndSource.FromHwnd(h)!;
        var state = Clicks.GetValue(window, _ => new ClickState());
        HwndSourceHook hook = (IntPtr handle, int message, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (message == 0x21) // WM_MOUSEACTIVATE
            {
                state.Previous = BeforeActivation(); state.Activating = true;
                Activate(handle);
                handled = true;
                // Desktop widgets are children of Explorer's desktop host. Keep our
                // own foreground anchor rather than trying to activate that host.
                return new IntPtr((GetWindowLong(handle, -16).ToInt64() & 0x40000000) != 0 ? 3 : 1);
            }
            if (message is 0x201 or 0x203 or 0x204 or 0x206 or 0x207 or 0x209)
            {
                if (!state.Activating) state.Previous = BeforeActivation();
                state.Activating = false;
                if (GetForegroundWindow() != handle) Activate(handle);
            }
            return IntPtr.Zero;
        };
        source.AddHook(hook);
        window.Closed += (_, _) => source.RemoveHook(hook);
    }

    internal static ImageSource? FileIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var location = Catalog.ShortcutIcon(path);
                if (!string.IsNullOrWhiteSpace(location.Path))
                {
                    SHDefExtractIcon(location.Path, location.Index, 0, out var large, out var small, 48);
                    try
                    {
                        if (large != IntPtr.Zero) { var bitmap = Imaging.CreateBitmapSourceFromHIcon(large, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); bitmap.Freeze(); return bitmap; }
                    }
                    finally { if (large != IntPtr.Zero) DestroyIcon(large); if (small != IntPtr.Zero) DestroyIcon(small); }
                }
            }
            if (path.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
            {
                var iid = typeof(IShellImage).GUID;
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var item) == 0)
                {
                    try
                    {
                        if (item.GetImage(new IconSize { X = 48, Y = 48 }, 5, out var bitmap) == 0)
                        {
                            try { var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); return image; }
                            finally { DeleteObject(bitmap); }
                        }
                    }
                    finally { Marshal.FinalReleaseComObject(item); }
                }
            }
            if (!path.Contains('\\') && !path.Contains('/') && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Prepend(Environment.GetFolderPath(Environment.SpecialFolder.Windows)))
                {
                    try { string candidate = Path.Combine(folder.Trim('"'), path); if (File.Exists(candidate)) { path = candidate; break; } } catch { }
                }
            }
            SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100);
            if (info.Icon == IntPtr.Zero) return null;
            try { var source = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48)); source.Freeze(); return source; }
            finally { DestroyIcon(info.Icon); }
        }
        catch { return null; }
    }
    internal static string ProcessPath(uint pid)
    {
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == IntPtr.Zero) return "";
        try { var b = new StringBuilder(32768); int size = b.Capacity; return QueryFullProcessImageName(handle, 0, b, ref size) ? b.ToString() : ""; }
        finally { CloseHandle(handle); }
    }

    internal static void Activate(IntPtr hwnd, bool minimizeIfActive = false, IntPtr foregroundBeforeClick = default)
    {
        if (!IsWindow(hwnd)) return;
        if ((GetWindowLong(hwnd, -16).ToInt64() & 0x40000000) != 0)
        {
            var anchor = Application.Current?.Windows.OfType<TopBar>().FirstOrDefault(w => w.IsVisible) ?? Application.Current?.Windows.OfType<TopBar>().FirstOrDefault();
            if (anchor != null) { if (!anchor.IsVisible) anchor.Show(); Activate(new WindowInteropHelper(anchor).Handle); }
            SetFocus(hwnd); return;
        }
        if (minimizeIfActive && (GetForegroundWindow() == hwnd || foregroundBeforeClick == hwnd)) { ShowWindowAsync(hwnd, 6); return; }
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, 9);
        if (GetForegroundWindow() == hwnd || SetForegroundWindow(hwnd)) return;
        // A shell shortcut can arrive while a game holds the foreground lock. Share
        // its input queue only for this requested handoff, then detach immediately.
        uint current = GetCurrentThreadId();
        uint foreground = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        bool attached = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
        try
        {
            if (!SetForegroundWindow(hwnd)) Log.Write("Foreground handoff denied for " + hwnd + " (" + Title(hwnd) + ").");
        }
        finally { if (attached) AttachThreadInput(current, foreground, false); }
    }

    internal static bool FullscreenOn(IntPtr monitor)
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || IsIconic(hwnd) || MonitorFromWindow(hwnd, 2) != monitor) return false;
        return IsFullscreenWindow(hwnd);
    }
    internal static bool IsFullscreenWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || IsIconic(hwnd)) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId) return false;
        string cls = Class(hwnd);
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        if (!GetWindowRect(hwnd, out var r)) return false;
        var m = Monitor(MonitorFromWindow(hwnd, 2)).Monitor;
        // A maximized framed window isn't an exclusive/borderless fullscreen window.
        if ((GetWindowLong(hwnd, -16).ToInt64() & 0x00C00000) != 0 && IsZoomed(hwnd)) return false;
        return r.Left <= m.Left + 1 && r.Top <= m.Top + 1 && r.Right >= m.Right - 1 && r.Bottom >= m.Bottom - 1;
    }

    internal static void KeepOnScreen(IntPtr hwnd)
    {
        if (!IsWindow(hwnd) || IsIconic(hwnd) || IsZoomed(hwnd) || !GetWindowRect(hwnd, out var r)) return;
        var m = Monitor(MonitorFromWindow(hwnd, 2)).Monitor;
        // Fit the outer window, including the title bar. Never change the game's display mode.
        int width = Math.Min(r.Width, m.Width), height = Math.Min(r.Height, m.Height);
        if (width <= 0 || height <= 0) return;
        int x = Math.Clamp(r.Left, m.Left, m.Right - width), y = Math.Clamp(r.Top, m.Top, m.Bottom - height);
        if (x != r.Left || y != r.Top || width != r.Width || height != r.Height)
        {
            SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, 0x4014);
            Log.Write("Fitted window to screen: " + Title(hwnd));
        }
    }

    internal static List<IntPtr> Taskbars()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { if (Class(h) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    internal static List<IntPtr> DesktopIconWindows()
    {
        var result = new List<IntPtr>();
        EnumWindows((root, _) => { if (Class(root) is "Progman" or "WorkerW") EnumChildWindows(root, (child, _) => { if (Class(child) == "SHELLDLL_DefView") result.Add(child); return true; }, IntPtr.Zero); return true; }, IntPtr.Zero);
        return result;
    }

    internal static uint TaskbarState()
    {
        var d = new AppBarData { Size = Marshal.SizeOf<AppBarData>(), Window = Taskbars().FirstOrDefault(h => Class(h) == "Shell_TrayWnd") };
        return (uint)SHAppBarMessage(4, ref d).ToUInt64();
    }
    internal static void SetTaskbarState(uint state)
    {
        var d = new AppBarData { Size = Marshal.SizeOf<AppBarData>(), Window = Taskbars().FirstOrDefault(h => Class(h) == "Shell_TrayWnd"), Param = new IntPtr(state) };
        SHAppBarMessage(10, ref d);
    }
    internal static void Media(byte code) { keybd_event(code, 0, 0, UIntPtr.Zero); keybd_event(code, 0, 2, UIntPtr.Zero); }
    internal static void Launch(string path, string? args = null)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Arguments = args ?? "" }); }
        catch (Exception ex) { Log.Write("Launch: " + ex.Message); MessageBox.Show("Couldn't open this app.\n\n" + ex.Message, "Harbor"); }
    }
}

internal sealed class AppBar : IDisposable
{
    readonly IntPtr hwnd;
    readonly uint callback;
    readonly uint taskbarCreated;
    internal readonly IntPtr MonitorHandle;
    readonly uint edge;
    int heightDip;
    readonly bool centered;
    readonly HwndSource source;
    bool positioning, disposed, suspended, registered, queued, overlay;
    double widthDip;
    string alignment = "center";
    internal AppBar(Window window, IntPtr monitor, uint edge, int height, bool center = false, bool overlay = false)
    {
        hwnd = new WindowInteropHelper(window).Handle;
        MonitorHandle = monitor; this.edge = edge; heightDip = height; centered = center;
        widthDip = window.Width; this.overlay = overlay;
        callback = Native.RegisterWindowMessage("Harbor.AppBar." + hwnd);
        taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        source = HwndSource.FromHwnd(hwnd)!;
        source.AddHook(Hook);
        Register();
        Position();
    }
    void Register()
    {
        if (registered || suspended || disposed || overlay) return;
        var d = Data(); d.Callback = callback;
        registered = Native.SHAppBarMessage(0, ref d) != UIntPtr.Zero;
    }
    internal bool Reserved => registered;
    internal void SetOverlay(bool value)
    {
        if (overlay == value) return;
        overlay = value;
        if (registered) { registered = false; var d = Data(); Native.SHAppBarMessage(1, ref d); }
        Register(); Position();
    }
    internal void SetSuspended(bool value)
    {
        if (suspended == value || disposed) return;
        suspended = value;
        if (value && registered)
        {
            registered = false;
            var d = Data(); Native.SHAppBarMessage(1, ref d);
        }
        else if (!value) { Register(); Position(); }
    }
    Native.AppBarData Data() => new() { Size = Marshal.SizeOf<Native.AppBarData>(), Window = hwnd, Edge = edge };
    internal void SetWidth(double width) { widthDip = width; Position(); }
    internal void Configure(int height, string align = "center") { if (heightDip == height && alignment == align) return; heightDip = height; alignment = align; Position(); }
    internal void Position()
    {
        if (positioning || disposed || suspended || (!overlay && !registered)) return;
        positioning = true;
        try
        {
            var m = Native.Monitor(MonitorHandle).Monitor;
            double scale = Native.GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0) scale = 1;
            int height = (int)Math.Round(heightDip * scale);
            var d = Data(); d.Rect = m;
            if (edge == 1) d.Rect.Bottom = d.Rect.Top + height;
            else d.Rect.Top = d.Rect.Bottom - height;
            if (!overlay)
            {
                Native.SHAppBarMessage(2, ref d);
                if (edge == 1) d.Rect.Bottom = d.Rect.Top + height;
                else d.Rect.Top = d.Rect.Bottom - height;
                Native.SHAppBarMessage(3, ref d);
            }
            int width = centered ? Math.Min(m.Width - 24, (int)Math.Round(widthDip * scale)) : d.Rect.Width;
            int x = centered ? alignment == "left" ? m.Left + 12 : alignment == "right" ? m.Right - width - 12 : m.Left + (m.Width - width) / 2 : d.Rect.Left;
            Native.GetWindowRect(hwnd, out var current);
            if (current.Left != x || current.Top != d.Rect.Top || current.Width != width || current.Height != height)
                Native.SetWindowPos(hwnd, IntPtr.Zero, x, d.Rect.Top, width, height, 0x14);
        }
        finally { positioning = false; }
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if ((uint)msg == taskbarCreated && !disposed)
        {
            registered = false; Register(); QueuePosition();
        }
        if (msg == callback && w.ToInt32() == 1 && !positioning) QueuePosition();
        if (msg == 0x02E0) QueuePosition();
        if (registered && msg == 6) { var d = Data(); d.Param = new IntPtr(w.ToInt64() & 0xffff); Native.SHAppBarMessage(6, ref d); }
        return IntPtr.Zero;
    }
    void QueuePosition()
    {
        if (queued || suspended || disposed) return;
        queued = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { queued = false; Position(); }));
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        source.RemoveHook(Hook);
        if (registered) { registered = false; var d = Data(); Native.SHAppBarMessage(1, ref d); }
    }
}
