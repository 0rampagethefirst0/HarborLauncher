using System;
using System.Runtime.InteropServices;

namespace Harbor;

public sealed class WindowEffectState
{
    public long Handle { get; set; }
    public uint ProcessId { get; set; }
    public long Token { get; set; }
    public bool Topmost { get; set; }
    public bool Layered { get; set; }
    public byte Alpha { get; set; } = 255;
    public uint ColorKey { get; set; }
    public uint LayeredFlags { get; set; }
    public bool RestoreTopmost { get; set; }
    public bool RestoreOpacity { get; set; }
}

internal static class WindowEffects
{
    internal static string LastError { get; private set; } = "";
    const long Layered = 0x80000, Topmost = 8;
    const string Property = "Harbor.WindowEffects.v1";
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetProp(IntPtr h, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetProp(IntPtr h, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr RemoveProp(IntPtr h, string name);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetLayeredWindowAttributes(IntPtr h, uint color, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetLayeredWindowAttributes(IntPtr h, out uint color, out byte alpha, out uint flags);
    internal static bool Matches(WindowEffectState saved)
    {
        var h = new IntPtr(saved.Handle); Native.GetWindowThreadProcessId(h, out uint pid);
        return Native.IsWindow(h) && pid == saved.ProcessId && saved.Token != 0 && GetProp(h, Property).ToInt64() == saved.Token;
    }
    internal static WindowEffectState? Capture(IntPtr h)
    {
        if (!Native.IsWindow(h)) return null;
        Native.GetWindowThreadProcessId(h, out uint pid); long style = Native.GetWindowLong(h, -20).ToInt64();
        var saved = new WindowEffectState { Handle = h.ToInt64(), ProcessId = pid, Token = Random.Shared.NextInt64(1, long.MaxValue), Topmost = (style & Topmost) != 0, Layered = (style & Layered) != 0 };
        if (saved.Layered && GetLayeredWindowAttributes(h, out uint color, out byte alpha, out uint flags)) { saved.ColorKey = color; saved.Alpha = alpha; saved.LayeredFlags = flags; }
        if (!SetProp(h, Property, new IntPtr(saved.Token))) return null;
        return saved;
    }
    internal static bool CanSetOpacity(WindowEffectState saved) => !saved.Layered || saved.LayeredFlags != 0;
    internal static bool SetTopmost(WindowEffectState saved, bool value)
    {
        var h = new IntPtr(saved.Handle); if (!Matches(saved)) return false;
        if (((Native.GetWindowLong(h, -20).ToInt64() & Topmost) != 0) == value) return true;
        return Native.SetWindowPos(h, new IntPtr(value ? -1 : -2), 0, 0, 0, 0, 0x4013);
    }
    internal static bool SetOpacity(WindowEffectState saved, double percent)
    {
        LastError = "";
        var h = new IntPtr(saved.Handle); if (!Matches(saved) || !CanSetOpacity(saved)) return false;
        byte target = (byte)Math.Max(1, Math.Round(Math.Clamp(double.IsFinite(percent) ? percent : 100, .5, 100) * 255 / 100));
        if (GetLayeredWindowAttributes(h, out _, out byte current, out uint currentFlags) && (currentFlags & 2) != 0 && current == target) return true;
        long style = Native.GetWindowLong(h, -20).ToInt64();
        if ((style & Layered) == 0) Native.SetWindowLong(h, -20, new IntPtr(style | Layered));
        int styleError = Marshal.GetLastPInvokeError();
        if ((Native.GetWindowLong(h, -20).ToInt64() & Layered) == 0) { LastError = styleError == 0 ? "This app manages its own transparency" : "Windows declined the opacity style (" + styleError + ")"; return false; }
        bool changed = SetLayeredWindowAttributes(h, saved.ColorKey, target, saved.LayeredFlags | 2);
        if (!changed) LastError = "Windows declined opacity (" + Marshal.GetLastPInvokeError() + ")";
        return changed;
    }
    internal static void Restore(WindowEffectState saved, string? effect = null)
    {
        if (!Matches(saved)) return;
        var h = new IntPtr(saved.Handle);
        if (saved.RestoreOpacity && effect is null or "window-opacity")
        {
            if (saved.Layered) SetLayeredWindowAttributes(h, saved.ColorKey, saved.Alpha, saved.LayeredFlags);
            else { Native.SetWindowLong(h, -20, new IntPtr(Native.GetWindowLong(h, -20).ToInt64() & ~Layered)); Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x4037); }
            saved.RestoreOpacity = false;
        }
        // Restoring the layered style writes the full extended style. Queue the
        // final z-order change afterwards so that write cannot re-add TOPMOST.
        if (saved.RestoreTopmost && effect is null or "window-topmost") { SetTopmost(saved, saved.Topmost); saved.RestoreTopmost = false; }
        if (!saved.RestoreTopmost && !saved.RestoreOpacity) RemoveProp(h, Property);
    }
}
