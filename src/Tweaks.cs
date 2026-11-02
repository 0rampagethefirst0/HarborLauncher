using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Harbor;

public sealed class RememberedWindow
{
    public string Monitor { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

internal sealed class Tweaks
{
    internal static readonly HashSet<string> WindowComponents = new() { "window-topmost", "window-opacity", "window-placement", "window-memory" };
    internal static readonly HashSet<string> Components = new(WindowComponents) { "dock-scroll-windows", "dock-middle-close", "dock-monitor-only", "dock-labels", "dock-wheel-volume", "toolbar-wheel-volume", "clock-seconds", "title-actions" };
    readonly Shell shell;
    readonly HashSet<(IntPtr Window, string Component)> effects = new();
    readonly Dictionary<(IntPtr Window, string Mod), uint> placed = new();
    readonly Dictionary<string, string> failures = new();
    readonly Dictionary<string, int> matches = new();
    readonly HashSet<(IntPtr Window, string Component)> declined = new();
    DateTime nextMemorySave;
    internal Tweaks(Shell shell) => this.shell = shell;
    internal ModDefinition? Module(string component) => shell.Mods.Enabled.LastOrDefault(m => m.Kind == "tweak" && m.Component == component);
    internal bool Has(string component) => Module(component) != null;
    internal string Status(ModDefinition mod) => !shell.Mods.EnabledMod(mod) ? "Disabled" : failures.TryGetValue(mod.Id, out var error) ? error : WindowComponents.Contains(mod.Component) ? (matches.GetValueOrDefault(mod.Id) + " matching windows · games excluded") : "Active";
    internal object Summary() => shell.Mods.Enabled.Where(m => m.Kind == "tweak").Select(m => new { m.Id, m.Component, Status = Status(m) }).ToArray();
    internal void Reload()
    {
        if (shell.Recovery != null) shell.Recovery.ReleaseAllEffects();
        effects.Clear(); placed.Clear(); failures.Clear(); matches.Clear(); declined.Clear();
    }
    internal static bool Wildcard(string pattern, string value)
    {
        // Bounded glob matching; patterns never execute code.
        if (pattern.Length > 512 || value.Length > 32000) return false;
        int budget = 50000;
        int p = 0, v = 0, star = -1, retry = 0;
        while (v < value.Length && --budget > 0)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(value[v]))) { p++; v++; }
            else if (p < pattern.Length && pattern[p] == '*') { star = p++; retry = v; }
            else if (star >= 0) { p = star + 1; v = ++retry; }
            else return false;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return budget > 0 && p == pattern.Length;
    }
    static IEnumerable<string> Lines(string value) => value.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(100);
    internal static bool Matches(string apps, string titles, string exclude, WindowEntry window)
    {
        bool App(string p) => Wildcard(p, window.Path) || Wildcard(p, window.ProcessName) || Wildcard(p, window.ProcessName + ".exe") || Wildcard(p, window.Key);
        if (!Lines(apps).Any(App)) return false;
        if (Lines(titles).Any() && !Lines(titles).Any(p => Wildcard(p, window.Title))) return false;
        return !Lines(exclude).Any(p => App(p) || Wildcard(p, window.Title));
    }
    internal bool Matches(ModDefinition mod, WindowEntry w) => Matches(shell.Mods.Value(mod, "apps"), shell.Mods.Value(mod, "titles"), shell.Mods.Value(mod, "exclude"), w);
    bool Eligible(WindowEntry w)
    {
        Native.GetWindowThreadProcessId(w.Handle, out uint pid);
        return Native.IsWindow(w.Handle) && Native.IsWindowVisible(w.Handle) && pid != Environment.ProcessId && !shell.Games.IsGame(w.Path) && !(shell.Games.Active && Native.MonitorFromWindow(shell.Games.ActiveWindow, 2) == w.Monitor) && Native.Class(w.Handle) is not "Progman" and not "WorkerW" && !Native.IsIconic(w.Handle);
    }
    internal void Tick()
    {
        if (shell.Recovery == null) return;
        var rules = shell.Mods.Enabled.Where(m => m.Kind == "tweak" && WindowComponents.Contains(m.Component)).ToList();
        var active = new HashSet<(IntPtr Window, string Component)>(); var seen = new HashSet<IntPtr>(); bool memoryChanged = false;
        foreach (var mod in rules) matches[mod.Id] = 0;
        foreach (var window in shell.Tracker.Windows)
        {
            if (!Eligible(window)) continue; seen.Add(window.Handle);
            var selected = rules.Where(m => Matches(m, window)).ToList(); foreach (var mod in selected) matches[mod.Id]++;
            foreach (var group in selected.GroupBy(m => m.Component).OrderBy(g => g.Key == "window-topmost" ? 1 : 0))
            {
                var mod = group.Last();
                try
                {
                    if (mod.Component is "window-topmost" or "window-opacity")
                    {
                        if (declined.Contains((window.Handle, mod.Component))) continue;
                        var snapshot = shell.Recovery.AcquireEffect(window.Handle, mod.Component);
                        if (snapshot == null) { failures[mod.Id] = "This app's window cannot be changed"; continue; }
                        bool ok = mod.Component == "window-topmost" ? WindowEffects.SetTopmost(snapshot, true) : WindowEffects.SetOpacity(snapshot, shell.Mods.Number(mod, "opacity", 90, .5, 100));
                        if (!ok) { string message = WindowEffects.LastError.Length > 0 ? WindowEffects.LastError : "Windows declined this window effect"; if (!failures.ContainsKey(mod.Id)) Log.Write("Mod " + mod.Id + ": " + message); failures[mod.Id] = message; declined.Add((window.Handle, mod.Component)); shell.Recovery.ReleaseEffect(window.Handle, mod.Component); }
                        else active.Add((window.Handle, mod.Component));
                    }
                    else
                    {
                        var key = (window.Handle, mod.Id); Native.GetWindowThreadProcessId(window.Handle, out uint pid);
                        if (!placed.TryGetValue(key, out uint prior) || prior != pid)
                        {
                            if (mod.Component == "window-placement") Place(mod, window);
                            else if (shell.Config.WindowMemory.TryGetValue(mod.Id + "|" + window.Key, out var saved)) RestorePosition(window, saved);
                            placed[key] = pid;
                        }
                        if (mod.Component == "window-memory" && DateTime.UtcNow >= nextMemorySave && !Native.IsZoomed(window.Handle) && Native.GetWindowRect(window.Handle, out var rect))
                        {
                            var info = Native.Monitor(Native.MonitorFromWindow(window.Handle, 2)); string id = mod.Id + "|" + window.Key;
                            var item = new RememberedWindow { Monitor = info.Device, X = rect.Left - info.Work.Left, Y = rect.Top - info.Work.Top, Width = rect.Width, Height = rect.Height };
                            if (!shell.Config.WindowMemory.TryGetValue(id, out var old) || old.Monitor != item.Monitor || old.X != item.X || old.Y != item.Y || old.Width != item.Width || old.Height != item.Height) { if (shell.Config.WindowMemory.Count >= 300 && !shell.Config.WindowMemory.ContainsKey(id)) shell.Config.WindowMemory.Remove(shell.Config.WindowMemory.Keys.First()); shell.Config.WindowMemory[id] = item; memoryChanged = true; }
                        }
                    }
                }
                catch (Exception ex) { if (!failures.ContainsKey(mod.Id)) Log.Write("Mod " + mod.Id + ": " + ex.Message); failures[mod.Id] = ex.Message; }
            }
        }
        foreach (var effect in effects.Where(e => !active.Contains(e)).ToList()) shell.Recovery.ReleaseEffect(effect.Window, effect.Component);
        effects.Clear(); effects.UnionWith(active);
        foreach (var key in placed.Keys.Where(k => !seen.Contains(k.Window)).ToArray()) placed.Remove(key);
        declined.RemoveWhere(k => !seen.Contains(k.Window));
        if (DateTime.UtcNow >= nextMemorySave) { nextMemorySave = DateTime.UtcNow.AddSeconds(10); if (memoryChanged) try { shell.Config.Save(); } catch (Exception ex) { Log.Write("Window position save: " + ex.Message); } }
    }
    void Place(ModDefinition mod, WindowEntry window)
    {
        string screen = shell.Mods.Value(mod, "monitor", "current"); var monitor = window.Monitor;
        var available = System.Windows.Forms.Screen.AllScreens;
        var target = screen == "primary" ? available.FirstOrDefault(s => s.Primary) : screen == "secondary" ? available.FirstOrDefault(s => !s.Primary) : null;
        if (target != null) monitor = Native.MonitorFromPoint(new Native.Point { X = target.Bounds.Left + 1, Y = target.Bounds.Top + 1 }, 2);
        var work = Native.Monitor(monitor).Work;
        var rect = Region(work, shell.Mods.Value(mod, "region", "center"), (int)shell.Mods.Number(mod, "gap", 12, 0, 80), shell.Mods.Number(mod, "width", 75, 20, 100), shell.Mods.Number(mod, "height", 80, 20, 100));
        shell.PlaceWindow(window.Handle, rect);
    }
    internal static Native.Rect Region(Native.Rect work, string region, int gap, double width = 75, double height = 80)
    {
        int x = work.Left + gap, y = work.Top + gap, w = Math.Max(80, work.Width - gap * 2), h = Math.Max(80, work.Height - gap * 2);
        if (region.Contains("left")) w = (w - gap) / 2;
        if (region.Contains("right")) { x += (w + gap) / 2; w = (w - gap) / 2; }
        if (region.Contains("top")) h = (h - gap) / 2;
        if (region.Contains("bottom")) { y += (h + gap) / 2; h = (h - gap) / 2; }
        if (region == "center") { int nw = (int)(w * width / 100), nh = (int)(h * height / 100); x += (w - nw) / 2; y += (h - nh) / 2; w = nw; h = nh; }
        return new Native.Rect { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }
    void RestorePosition(WindowEntry window, RememberedWindow saved)
    {
        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == saved.Monitor);
        var monitor = screen == null ? window.Monitor : Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
        var work = Native.Monitor(monitor).Work; int width = Math.Clamp(saved.Width, 120, Math.Max(120, work.Width)), height = Math.Clamp(saved.Height, 80, Math.Max(80, work.Height));
        int x = Math.Clamp(work.Left + saved.X, work.Left, Math.Max(work.Left, work.Right - width)), y = Math.Clamp(work.Top + saved.Y, work.Top, Math.Max(work.Top, work.Bottom - height));
        shell.PlaceWindow(window.Handle, new Native.Rect { Left = x, Top = y, Right = x + width, Bottom = y + height });
    }
}
