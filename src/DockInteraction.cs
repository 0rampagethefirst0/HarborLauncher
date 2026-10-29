using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Harbor;

internal sealed class DockLease
{
    internal bool Granted { get; private set; }
    long until;
    internal void Grant(long now, int delay) { Granted = true; until = now + delay; }
    internal bool BeginRightClick(long now, int delay) { bool activateOnly = !Granted; Grant(now, delay); return activateOnly; }
    internal bool Update(long now, bool pointerInside, bool busy, int delay)
    {
        if (!Granted) return false;
        if (pointerInside || busy) until = now + delay;
        if (now >= until) Granted = false;
        return Granted;
    }
    internal void Reset() => Granted = false;
}
internal sealed class DockTyping
{
    readonly Dictionary<IntPtr, long> until = new();
    internal void Mark(IntPtr monitor, long now) => until[monitor] = now + 1500;
    internal bool Active(IntPtr monitor, long now) => until.TryGetValue(monitor, out long end) && now < end;
    internal void Clear(IntPtr monitor) => until.Remove(monitor);
    internal void Prune(long now) { foreach (var key in until.Keys.Where(k => until[k] <= now).ToArray()) until.Remove(key); }
    internal static bool TextKey(uint key) => key is 8 or 9 or 13 or 32 or 46 or 0xE5 || key is >= 0x25 and <= 0x28 or >= 0x30 and <= 0x5A or >= 0x60 and <= 0x6F or >= 0xBA and <= 0xE2;
}

internal sealed class DockInteraction : IDisposable
{
    delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct MouseData { public Native.Point Point; public uint Data, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int kind, MouseProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.Point point);
    readonly Shell shell;
    readonly Dictionary<Dock, DockLease> docks = new();
    readonly Dictionary<Dock, Native.Rect> bounds = new();
    Dock[] registered = Array.Empty<Dock>();
    readonly List<(IntPtr Monitor, Native.Rect Frame)> appFrames = new();
    readonly DockTyping typing = new();
    long nextFrameRead;
    readonly DispatcherTimer timer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(80) };
    readonly MouseProc callback;
    readonly IntPtr hook;
    Dock? pendingRight;
    bool swallowRightUp, disposed, failed;
    internal bool HookReady => hook != IntPtr.Zero;
    internal bool Enabled => shell.Config.DockHoverThrough && HookReady && !failed;
    internal DockInteraction(Shell shell, bool live)
    {
        this.shell = shell; callback = (code, message, data) => { try { return Handle(code, message, data); } catch (Exception ex) { failed = true; System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => Log.Write("Dock hover-through disabled after an input error: " + ex.Message))); return CallNextHookEx(hook, code, message, data); } };
        if (live) { hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0); if (!HookReady) Log.Write("Dock hover-through mouse hook unavailable; using the reserved interactive dock."); timer.Tick += (_, _) => Refresh(); timer.Start(); }
    }
    internal void Register(Dock dock) { docks[dock] = new(); registered = docks.Keys.ToArray(); UpdateBounds(dock); }
    internal void Unregister(Dock dock) { docks.Remove(dock); bounds.Remove(dock); registered = docks.Keys.ToArray(); if (pendingRight == dock) pendingRight = null; }
    internal void Touch(Dock dock)
    {
        if (!docks.TryGetValue(dock, out var lease)) return;
        typing.Clear(dock.MonitorHandle);
        lease.Grant(Environment.TickCount64, shell.Config.DockInteractionDelay); dock.SetInputMode(false, shell.Config.DockFadeOpacity);
    }
    internal void NotifyTyping()
    {
        if (!Enabled || !shell.Config.DockQuietWhileTyping) return;
        var foreground = Native.GetForegroundWindow(); if (foreground == IntPtr.Zero) return;
        Native.GetWindowThreadProcessId(foreground, out uint pid);
        if (pid == Environment.ProcessId && !System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().Any(w => w.ShowInTaskbar && w.IsVisible && new WindowInteropHelper(w).Handle == foreground)) return;
        if (Native.Class(foreground) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return;
        var monitor = Native.MonitorFromWindow(foreground, 2); typing.Mark(monitor, Environment.TickCount64);
        foreach (var dock in registered) if (dock.MonitorHandle == monitor && dock.IsVisible && docks.TryGetValue(dock, out var lease)) { lease.Reset(); dock.SetInputMode(true, shell.Config.DockFadeOpacity); }
    }
    internal void Refresh()
    {
        if (disposed) return;
        typing.Prune(Environment.TickCount64);
        Native.GetCursorPos(out var cursor);
        foreach (var pair in docks.ToArray()) { UpdateBounds(pair.Key); Update(pair.Key, pair.Value, cursor); }
    }
    void UpdateBounds(Dock dock) { var h = new WindowInteropHelper(dock).Handle; if (h != IntPtr.Zero && Native.GetWindowRect(h, out var rect)) bounds[dock] = rect; }
    static bool Contains(Native.Rect rect, Native.Point point) => point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
    bool CoversApp(Dock dock, Native.Point point)
    {
        if (Environment.TickCount64 < nextFrameRead) return appFrames.Any(w => w.Monitor == dock.MonitorHandle && Contains(w.Frame, point));
        appFrames.Clear(); nextFrameRead = Environment.TickCount64 + 150;
        foreach (var window in shell.Tracker.Windows)
        {
            if (Native.IsIconic(window.Handle)) continue;
            Native.GetWindowThreadProcessId(window.Handle, out uint pid); if (pid == Environment.ProcessId) continue;
            if (Native.GetWindowRect(window.Handle, out var outer)) appFrames.Add((window.Monitor, Native.VisibleBounds(window.Handle, outer)));
        }
        foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
        {
            if (!window.ShowInTaskbar || !window.IsVisible) continue;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero && Native.GetWindowRect(handle, out var outer)) appFrames.Add((Native.MonitorFromWindow(handle, 2), Native.VisibleBounds(handle, outer)));
        }
        return appFrames.Any(w => w.Monitor == dock.MonitorHandle && Contains(w.Frame, point));
    }
    bool CoveredByPopup(Dock dock, Native.Point point)
    {
        var target = Native.GetAncestor(WindowFromPoint(point), 2); var own = new WindowInteropHelper(dock).Handle;
        if (target == IntPtr.Zero || target == own) return false;
        int count = 0; for (var h = Native.GetWindow(own, 3); h != IntPtr.Zero && count++ < 128; h = Native.GetWindow(h, 3)) if (h == target) return true;
        return false;
    }
    void Update(Dock dock, DockLease lease, Native.Point cursor)
    {
        if (!Enabled || !dock.IsVisible) { lease.Reset(); dock.SetInputMode(false, shell.Config.DockFadeOpacity); return; }
        bool inside = bounds.TryGetValue(dock, out var rect) && Contains(rect, cursor);
        bool interactive = lease.Update(Environment.TickCount64, inside, dock.MenuOpen || dock.IsMouseCaptureWithin || shell.DockMenuOpen, shell.Config.DockInteractionDelay);
        dock.SetInputMode((shell.Config.DockQuietWhileTyping && typing.Active(dock.MonitorHandle, Environment.TickCount64)) || (!interactive && inside && CoversApp(dock, cursor)), shell.Config.DockFadeOpacity);
    }
    IntPtr Handle(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || disposed) return CallNextHookEx(hook, code, message, data);
        int kind = message.ToInt32();
        if (kind == 0x205 && swallowRightUp)
        {
            swallowRightUp = false; var dock = pendingRight; pendingRight = null;
            var point = Marshal.PtrToStructure<MouseData>(data).Point;
            if (dock != null && dock.IsVisible && bounds.TryGetValue(dock, out var rect) && Contains(rect, point)) Touch(dock);
            return new IntPtr(1);
        }
        if (!Enabled || kind is not 0x200 and not 0x204) return CallNextHookEx(hook, code, message, data);
        var cursor = Marshal.PtrToStructure<MouseData>(data).Point;
        foreach (var dock in registered)
        {
            if (!docks.TryGetValue(dock, out var lease)) continue;
            if (kind == 0x204) UpdateBounds(dock);
            if (!dock.IsVisible || !bounds.TryGetValue(dock, out var rect)) continue;
            bool inside = Contains(rect, cursor);
            if (kind == 0x200)
            {
                if ((inside && !dock.InputPassThrough) || (!inside && dock.InputPassThrough)) Update(dock, lease, cursor);
                continue;
            }
            if (!inside || dock.MenuOpen || shell.DockMenuOpen || CoveredByPopup(dock, cursor)) continue;
            bool activateOnly = lease.BeginRightClick(Environment.TickCount64, shell.Config.DockInteractionDelay); Touch(dock);
            if (activateOnly) { pendingRight = dock; swallowRightUp = true; return new IntPtr(1); }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    internal object Status() => new { Enabled, HookReady, FadeOpacity = shell.Config.DockFadeOpacity, HoldMilliseconds = shell.Config.DockInteractionDelay, TypingQuietMilliseconds = 1500, Docks = docks.Select(p => new { Monitor = Native.Monitor(p.Key.MonitorHandle).Device, PassThrough = p.Key.InputPassThrough, Interactive = p.Value.Granted, Typing = typing.Active(p.Key.MonitorHandle, Environment.TickCount64), p.Key.MenuOpen }).ToArray() };
    public void Dispose()
    {
        disposed = true; timer.Stop(); if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        foreach (var dock in docks.Keys.ToArray()) dock.SetInputMode(false, shell.Config.DockFadeOpacity);
        docks.Clear(); bounds.Clear(); registered = Array.Empty<Dock>(); appFrames.Clear();
    }
}
