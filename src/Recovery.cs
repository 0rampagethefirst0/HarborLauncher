using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace Harbor;

internal sealed class RecoveryState
{
    public int ProcessId { get; set; }
    public long StartTicks { get; set; }
    public uint TaskbarState { get; set; }
    public bool Hidden { get; set; }
    public DateTime HeartbeatUtc { get; set; }
    public long HeartbeatAwakeTicks { get; set; }
    public List<SavedWorkArea> WorkAreas { get; set; } = new();
    public bool DesktopReplacement { get; set; }
    public List<long> DesktopWindows { get; set; } = new();
    public List<WindowEffectState> WindowEffects { get; set; } = new();
    public bool ShellSession { get; set; }
    public bool ShellReady { get; set; }
}
internal sealed class SavedWorkArea
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}

internal sealed class Recovery : IDisposable
{
    const long TimeoutTicks = 20 * TimeSpan.TicksPerSecond;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool QueryUnbiasedInterruptTime(out long ticks);
    internal static long AwakeTicks()
    {
        // This monotonic Windows clock excludes sleep and hibernation.
        if (!QueryUnbiasedInterruptTime(out long ticks)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ticks;
    }
    static string StatePath => Path.Combine(Settings.DataDir, "recovery.json");
    readonly RecoveryState state;
    internal bool Hidden => state.Hidden;
    internal bool ShouldHideTaskbars => state.Hidden && DateTime.UtcNow >= revealUntil;
    DateTime revealUntil;
    int ticks;
    internal Recovery(bool watchdog = true)
    {
        if (!watchdog && Settings.TestDataDir == null) throw new InvalidOperationException("A recovery check requires an isolated profile.");
        RecoverStale();
        using var p = Process.GetCurrentProcess();
        state = new RecoveryState { ProcessId = p.Id, StartTicks = p.StartTime.ToUniversalTime().Ticks, TaskbarState = Native.TaskbarState(), ShellSession = Harbor.ShellSession.IsShellProcess };
        CaptureWorkAreas();
        Write(true);
        if (!watchdog) return;
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--watchdog"); start.ArgumentList.Add(p.Id.ToString()); start.ArgumentList.Add(state.StartTicks.ToString());
        Process.Start(start)?.Dispose();
    }
    int writeFailures;
    internal void MarkShellReady() { state.ShellReady = true; Write(true); }
    internal WindowEffectState? AcquireEffect(IntPtr window, string effect)
    {
        state.WindowEffects.RemoveAll(s => !Harbor.WindowEffects.Matches(s));
        var saved = state.WindowEffects.FirstOrDefault(s => s.Handle == window.ToInt64());
        bool created = saved == null;
        if (saved == null)
        {
            if (state.WindowEffects.Count >= 256) return null;
            saved = Harbor.WindowEffects.Capture(window); if (saved == null) return null;
            state.WindowEffects.Add(saved);
        }
        if (effect == "window-opacity" && !Harbor.WindowEffects.CanSetOpacity(saved)) { if (created) { Harbor.WindowEffects.Restore(saved); state.WindowEffects.Remove(saved); } return null; }
        bool already = effect == "window-topmost" ? saved.RestoreTopmost : saved.RestoreOpacity;
        if (!already)
        {
            if (effect == "window-topmost") saved.RestoreTopmost = true; else saved.RestoreOpacity = true;
            try { Write(true); }
            catch
            {
                if (effect == "window-topmost") saved.RestoreTopmost = false; else saved.RestoreOpacity = false;
                if (created) { Harbor.WindowEffects.Restore(saved); state.WindowEffects.Remove(saved); }
                throw;
            }
        }
        return saved;
    }
    internal void ReleaseEffect(IntPtr window, string effect)
    {
        var saved = state.WindowEffects.FirstOrDefault(s => s.Handle == window.ToInt64()); if (saved == null) return;
        Harbor.WindowEffects.Restore(saved, effect);
        if (!saved.RestoreTopmost && !saved.RestoreOpacity) state.WindowEffects.Remove(saved);
        Write();
    }
    internal void ReleaseAllEffects()
    {
        foreach (var saved in state.WindowEffects) Harbor.WindowEffects.Restore(saved);
        state.WindowEffects.Clear(); Write();
    }
    void Write(bool required = false)
    {
        state.HeartbeatUtc = DateTime.UtcNow;
        state.HeartbeatAwakeTicks = AwakeTicks();
        string json = JsonSerializer.Serialize(state);
        if (required) { AtomicFile.WriteText(StatePath, json); writeFailures = 0; return; }
        if (AtomicFile.TryWriteText(StatePath, json)) { if (writeFailures > 0) Log.Write("Recovery-state writes resumed."); writeFailures = 0; }
        else if (++writeFailures == 1) Log.Write("Recovery-state file is temporarily locked; heartbeat write will retry.");
    }
    internal void SetReplacement(bool enabled)
    {
        if (enabled == state.Hidden) return;
        if (enabled)
        {
            CaptureWorkAreas();
            state.TaskbarState = Native.TaskbarState();
            state.Hidden = true;
            Write(true); // The helper must have restoration data before any shell change.
            Native.SetTaskbarState(state.TaskbarState | 1);
            foreach (var h in Native.Taskbars()) Native.ShowWindow(h, 0);
        }
        else
        {
            Restore(state);
            state.Hidden = false;
            Write();
        }
        Log.Write("Windows taskbar replacement: " + enabled);
    }
    void CaptureWorkAreas()
    {
        state.WorkAreas = System.Windows.Forms.Screen.AllScreens.Select(s => new SavedWorkArea { Left = s.WorkingArea.Left, Top = s.WorkingArea.Top, Right = s.WorkingArea.Right, Bottom = s.WorkingArea.Bottom }).ToList();
    }
    internal void UpdateWorkArea(IntPtr monitor, bool hide, bool hasBars, int top = 38, int bottom = 80)
    {
        if (!state.Hidden) return;
        var info = Native.Monitor(monitor); var desired = info.Monitor;
        double scale = Native.Scale(monitor);
        if (!hide && hasBars) { desired.Top += (int)Math.Round(top * scale); desired.Bottom -= (int)Math.Round(bottom * scale); }
        var current = info.Work;
        if (current.Left != desired.Left || current.Top != desired.Top || current.Right != desired.Right || current.Bottom != desired.Bottom)
        {
            Native.SetWorkArea(0x2F, 0, ref desired, 0);
        }
    }
    internal void RevealTaskbar()
    {
        revealUntil = DateTime.UtcNow.AddSeconds(12);
        foreach (var h in Native.Taskbars()) Native.ShowWindow(h, 5);
    }
    internal void SetDesktop(bool enabled)
    {
        state.DesktopReplacement = enabled;
        if (enabled)
        {
            foreach (var h in Native.DesktopIconWindows().Where(Native.IsWindowVisible))
            {
                if (!state.DesktopWindows.Contains(h.ToInt64())) state.DesktopWindows.Add(h.ToInt64());
                Write(true); Native.ShowWindow(h, 0);
            }
        }
        else
        {
            RestoreDesktop(state); state.DesktopWindows.Clear(); Write();
        }
    }
    static void RestoreDesktop(RecoveryState saved)
    {
        foreach (long id in saved.DesktopWindows ?? new()) { var h = new IntPtr(id); if (Native.IsWindow(h) && Native.Class(h) == "SHELLDLL_DefView") Native.ShowWindow(h, 5); }
    }
    internal void Tick()
    {
        if (++ticks % 2 != 0) return;
        Write();
        if (state.DesktopReplacement) SetDesktop(true);
        if (state.Hidden && DateTime.UtcNow >= revealUntil)
        {
            foreach (var h in Native.Taskbars()) if (Native.IsWindowVisible(h)) Native.ShowWindow(h, 0);
        }
    }
    public void Dispose()
    {
        ReleaseAllEffects();
        if (state.Hidden) Restore(state);
        else RestoreDesktop(state);
        state.Hidden = false;
        try { Write(); File.Delete(StatePath); } catch { }
    }
    static void Restore(RecoveryState state)
    {
        RestoreDesktop(state);
        Native.SetTaskbarState(state.TaskbarState);
        foreach (var h in Native.Taskbars()) Native.ShowWindow(h, 5);
        foreach (var area in state.WorkAreas ?? new())
        {
            var rect = new Native.Rect { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom };
            Native.SetWorkArea(0x2F, 0, ref rect, 0);
        }
        Native.SendNotifyMessage(new IntPtr(0xffff), 0x1A, new IntPtr(0x2F), IntPtr.Zero);
        foreach (var saved in state.WindowEffects ?? new()) Harbor.WindowEffects.Restore(saved);
        Log.Write("Windows taskbar restored.");
    }
    internal static void RecoverStale()
    {
        var prior = Read();
        if (prior != null && (prior.Hidden || prior.DesktopReplacement || prior.WindowEffects?.Count > 0) && !Alive(prior.ProcessId, prior.StartTicks)) { Restore(prior); try { File.Delete(StatePath); } catch { } }
    }
    static RecoveryState? Read() { try { return JsonSerializer.Deserialize<RecoveryState>(AtomicFile.ReadText(StatePath)); } catch { return null; } }
    static bool Alive(int pid, long ticks)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited && p.StartTime.ToUniversalTime().Ticks == ticks; } catch { return false; }
    }
    internal static void Watch(int pid, long start)
    {
        Log.Write("Recovery helper watching " + pid);
        long? noStateSince = null;
        while (Alive(pid, start))
        {
            Thread.Sleep(1000);
            long now = AwakeTicks();
            var current = Read();
            if (current == null)
            {
                noStateSince ??= now;
                if (now - noStateSince.Value < TimeoutTicks) continue;
                // Normal cleanup removed the state, but a leftover message-loop thread survived.
                try { using var p = Process.GetProcessById(pid); if (p.StartTime.ToUniversalTime().Ticks == start) p.Kill(); } catch { }
                break;
            }
            noStateSince = null;
            if (current.ProcessId != pid || current.StartTicks != start) break;
            if (now - current.HeartbeatAwakeTicks < TimeoutTicks) continue;
            // A frozen UI must not leave unusable shell surfaces on the screen.
            Log.Write("UI heartbeat expired after 20 seconds awake; restoring the desktop.");
            if (current.Hidden || current.DesktopReplacement || current.WindowEffects?.Count > 0) Restore(current);
            try { using var p = Process.GetProcessById(pid); if (p.StartTime.ToUniversalTime().Ticks == start) p.Kill(); } catch { }
            break;
        }
        var state = Read();
        if (state != null && state.ProcessId == pid && state.StartTicks == start)
        {
            if (state.Hidden || state.DesktopReplacement || state.WindowEffects?.Count > 0) Restore(state);
            try { File.Delete(StatePath); } catch { }
        }
        Log.Write("Recovery helper finished.");
        // The supervisor restarts failures. If it disappeared too, leave a usable
        // desktop instead of a blank screen.
        if (state?.ShellSession == true && !Harbor.ShellSession.HostRunning) Harbor.ShellSession.EnsureExplorer();
    }
    internal static void ForceRestore()
    {
        var current = Read();
        if (current != null) Restore(current);
        else foreach (var h in Native.Taskbars()) Native.ShowWindow(h, 5);
    }
}
