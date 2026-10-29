using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Harbor;

internal sealed class WindowSpace
{
    readonly Shell shell;
    readonly Dictionary<IntPtr, (uint Pid, bool Minimized)> seen = new();
    readonly Dictionary<IntPtr, Native.Rect> workAreas = new();
    readonly Dictionary<IntPtr, DateTime> nextAttempt = new();
    readonly Dictionary<IntPtr, (Native.Rect Observed, Native.Rect Work)> attempts = new();
    internal int Adjustments { get; private set; }
    internal WindowSpace(Shell shell) => this.shell = shell;
    internal void Tick()
    {
        var changed = new HashSet<IntPtr>();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
            var work = Native.Monitor(monitor).Work;
            if (!workAreas.TryGetValue(monitor, out var old) || !Same(work, old)) changed.Add(monitor);
            workAreas[monitor] = work;
        }
        var live = new HashSet<IntPtr>();
        foreach (var window in shell.Tracker.Windows)
        {
            live.Add(window.Handle); Native.GetWindowThreadProcessId(window.Handle, out uint pid);
            bool minimized = Native.IsIconic(window.Handle);
            bool fresh = !seen.TryGetValue(window.Handle, out var previous) || previous.Pid != pid;
            bool restored = !fresh && previous.Minimized && !minimized;
            seen[window.Handle] = (pid, minimized);
            if (!shell.Config.ReplaceTaskbar || minimized || pid == Environment.ProcessId || shell.Games.IsGame(window.Path) || shell.HideBars(window.Monitor) || Native.IsFullscreenWindow(window.Handle)) continue;
            var info = Native.Monitor(window.Monitor);
            if (Same(info.Work, info.Monitor)) continue;
            if (!Native.IsZoomed(window.Handle) && !fresh && !restored && !changed.Contains(window.Monitor)) continue;
            if (nextAttempt.TryGetValue(window.Handle, out var due) && DateTime.UtcNow < due) continue;
            if (!Native.GetWindowRect(window.Handle, out var outer)) continue;
            if (!fresh && !restored && !changed.Contains(window.Monitor) && attempts.TryGetValue(window.Handle, out var attempted) && Same(attempted.Observed, outer) && Same(attempted.Work, info.Work)) continue;
            var visible = Native.VisibleBounds(window.Handle, outer);
            var target = Fit(outer, visible, info.Work, Native.IsZoomed(window.Handle));
            if (Same(target, outer, 1)) { attempts.Remove(window.Handle); nextAttempt.Remove(window.Handle); continue; }
            // Preserve maximize/restore state and focus. Async positioning cannot
            // hold up Harbor while an external application handles its messages.
            if (Native.SetWindowPos(window.Handle, IntPtr.Zero, target.Left, target.Top, target.Width, target.Height, 0x4014)) Adjustments++;
            nextAttempt[window.Handle] = DateTime.UtcNow.AddSeconds(2);
            attempts[window.Handle] = (outer, info.Work);
        }
        foreach (var h in seen.Keys.Where(h => !live.Contains(h)).ToArray()) { seen.Remove(h); nextAttempt.Remove(h); attempts.Remove(h); }
    }
    internal static Native.Rect Fit(Native.Rect outer, Native.Rect visible, Native.Rect work, bool maximized)
    {
        int leftBorder = Math.Clamp(visible.Left - outer.Left, 0, 32), rightBorder = Math.Clamp(outer.Right - visible.Right, 0, 32);
        int topBorder = Math.Clamp(visible.Top - outer.Top, 0, 32), bottomBorder = Math.Clamp(outer.Bottom - visible.Bottom, 0, 32);
        int width = maximized ? work.Width : Math.Min(visible.Width, work.Width), height = maximized ? work.Height : Math.Min(visible.Height, work.Height);
        width = Math.Max(1, width); height = Math.Max(1, height);
        int x = maximized ? work.Left : Math.Clamp(visible.Left, work.Left, Math.Max(work.Left, work.Right - width));
        int y = maximized ? work.Top : Math.Clamp(visible.Top, work.Top, Math.Max(work.Top, work.Bottom - height));
        return new Native.Rect { Left = x - leftBorder, Top = y - topBorder, Right = x + width + rightBorder, Bottom = y + height + bottomBorder };
    }
    internal static bool Same(Native.Rect a, Native.Rect b, int tolerance = 0) => Math.Abs(a.Left - b.Left) <= tolerance && Math.Abs(a.Top - b.Top) <= tolerance && Math.Abs(a.Right - b.Right) <= tolerance && Math.Abs(a.Bottom - b.Bottom) <= tolerance;
}
