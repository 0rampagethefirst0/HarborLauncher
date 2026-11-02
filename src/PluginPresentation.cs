using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace Harbor;

internal static class PluginPresentation
{
    internal const string Edge = "Edge tab", Floating = "Floating control", Dock = "Dock button", Desktop = "Desktop panel";
    internal static readonly string[] Modes = { Edge, Floating, Dock, Desktop };
    internal static string Mode(Mods mods, ModDefinition mod)
    {
        string value = mods.Value(mod, "access");
        return Modes.Contains(value) ? value : mod.Id.StartsWith("harbor.") && mod.Author == "Harbor" ? Edge : Desktop;
    }
    internal static string Icon(ModDefinition mod) => Key(mod) switch {
        "clock" or "world-clock" or "uptime" => "clock", "system" or "resource-desk" => "stats", "network" => "network", "storage" => "drive",
        "notes" => "file", "tasks" or "day-planner" => "tasks", "focus" or "stopwatch" => "timer", "calculator" or "converter" => "calculator",
        "folders" => "folder", "clipboard" => "copy", "layouts" or "window-desk" => "grid", "launchpad" => "apps", "sound-desk" => "volume", _ => "mods"
    };
    internal static string Key(ModDefinition mod) => mod.Id.StartsWith("harbor.") ? mod.Id[7..] : mod.Component;
    internal static string ShortName(ModDefinition mod) => Key(mod) switch {
        "clock" => "Clock", "world-clock" => "World time", "system" => "CPU", "network" => "Network", "storage" => "Drives", "uptime" => "Uptime",
        "notes" => "Notes", "tasks" => "Tasks", "focus" => "Focus", "stopwatch" => "Stopwatch", "calculator" => "Calculate", "converter" => "Convert",
        "folders" => "Folders", "clipboard" => "Clipboard", "layouts" => "Layouts", "launchpad" => "Apps", "sound-desk" => "Sound",
        "resource-desk" => "Resources", "day-planner" => "Today", "window-desk" => "Windows", _ => mod.Name
    };
    internal static string Reading(Shell shell, ModDefinition mod, bool sample = false)
    {
        try
        {
            string custom = shell.Mods.Value(mod, "compact-format");
            if (!string.IsNullOrWhiteSpace(custom)) return new WidgetLayout(shell, mod, true, sample ? new WidgetDemoData { Phase = .5 } : null).Expand(custom);
            string key = Key(mod);
            switch (key)
            {
                case "clock": return (sample ? new DateTime(2026, 10, 29, 8, 25, 0) : DateTime.Now).ToString(shell.Mods.Value(mod, "format", "HH:mm"));
                case "world-clock": return TimeZoneInfo.ConvertTime(sample ? new DateTimeOffset(2026, 10, 29, 12, 25, 0, TimeSpan.Zero) : DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(shell.Mods.Value(mod, "zone", "Tokyo Standard Time"))).ToString(shell.Mods.Value(mod, "format", "HH:mm"));
                case "system": case "resource-desk":
                    string cpu = "CPU " + (sample ? 24 : shell.CpuUsage).ToString("0") + "%", ram = "RAM " + (sample ? 42 : shell.MemoryUsage) + "%";
                    return shell.Mods.Value(mod, "meter-mode", "CPU + RAM") switch { "CPU" => cpu, "RAM" => ram, _ => cpu + " · " + ram };
                case "storage":
                    var names = shell.Mods.Value(mod, "drives", "C:").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    string mode = shell.Mods.Value(mod, "drive-mode", "All drives");
                    if (sample) return mode == "First drive" ? "C: 642 GB" : "C: 642 GB · D: 1.2 TB";
                    var drives = names.Select(name => { try { var d = new DriveInfo(name); return d.IsReady ? d : null; } catch { return null; } }).OfType<DriveInfo>().ToArray();
                    if (drives.Length == 0) return "No drive";
                    if (mode is "First drive" or "Cycle drives") drives = new[] { drives[mode == "First drive" ? 0 : (int)(Environment.TickCount64 / 4000 % drives.Length)] };
                    return string.Join(" · ", drives.Select(d => d.Name.TrimEnd('\\') + " " + (mode == "Used percent" ? (100d * (d.TotalSize - d.AvailableFreeSpace) / d.TotalSize).ToString("0") + "%" : FileTools.Size(d.AvailableFreeSpace))));
                case "uptime": var uptime = sample ? TimeSpan.FromHours(52) : TimeSpan.FromMilliseconds(Environment.TickCount64); return uptime.TotalDays >= 1 ? (int)uptime.TotalDays + "d " + uptime.Hours + "h" : uptime.Hours + "h " + uptime.Minutes + "m";
                case "tasks": var items = shell.Mods.Value(mod, "items").Split('\n', StringSplitOptions.RemoveEmptyEntries); return "Tasks " + Enumerable.Range(0, items.Length).Count(i => shell.Mods.Value(mod, "checked-" + i) == "true") + "/" + items.Length;
                case "focus": case "stopwatch":
                    if (shell.ModRuntime.TryGetValue(mod.Id, out var runtime) && runtime is TimerState state) { double seconds = key == "focus" ? Math.Max(0, state.Seconds - state.Watch.Elapsed.TotalSeconds) : state.Watch.Elapsed.TotalSeconds; return TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss"); }
                    return key == "focus" ? shell.Mods.Value(mod, "minutes", "25") + ":00" : "00:00";
                case "clipboard": return shell.ModRuntime.TryGetValue(mod.Id, out var history) && history is List<string> values ? "Clips " + values.Count : "Clipboard";
                default: return ShortName(mod);
            }
        }
        catch { return ShortName(mod); }
    }
    internal static Rect Place(Rect work, Size size, string mode, string edge, double x, double y)
    {
        x = double.IsFinite(x) ? x : .5; y = double.IsFinite(y) ? y : .5;
        double left = Math.Max(0, work.Width - size.Width - 12), top = Math.Max(0, work.Height - size.Height - 12);
        return new Rect(work.Left + 6 + (mode == Edge ? edge == "Left" ? 0 : left : Math.Clamp(x, 0, 1) * left), work.Top + 6 + Math.Clamp(y, 0, 1) * top, Math.Min(size.Width, work.Width), Math.Min(size.Height, work.Height));
    }
    internal static bool IsDesktop(Mods mods, ModDefinition mod) => mod.Kind == "widget" && Mode(mods, mod) == Desktop;
    internal static Rect[] PackVertical(IReadOnlyList<Rect> desired, Rect area, double gap)
    {
        var result = desired.ToArray(); var order = Enumerable.Range(0, result.Length).OrderBy(i => result[i].Top).ToArray();
        double required = result.Sum(r => r.Height) + Math.Max(0, result.Length - 1) * gap;
        if (required > area.Height - 12) return result;
        double cursor = area.Top + 6;
        for (int n = 0; n < order.Length; n++) { int i = order[n]; double remaining = order.Skip(n).Sum(j => result[j].Height) + (order.Length - n - 1) * gap; double top = Math.Clamp(result[i].Top, cursor, area.Bottom - 6 - remaining); result[i].Y = top; cursor = result[i].Bottom + gap; }
        return result;
    }
    internal static string Content(Mods mods, ModDefinition mod) => mods.Value(mod, "compact-reading", "Live reading") switch { "No" or "Name" => "Name", "Icon only" => "Icon only", _ => "Live reading" };
    internal static void SetMode(Shell shell, ModDefinition mod, string mode)
    {
        if (!Modes.Contains(mode)) return;
        shell.Mods.Set(mod, "access", mode, false); shell.RebuildMods();
    }
    internal static void SyncDock(Shell shell)
    {
        var active = shell.Mods.Enabled.Where(m => m.Kind == "widget" && Mode(shell.Mods, m) == Dock).ToDictionary(m => m.Id);
        foreach (var item in shell.Config.TopDockItems.Where(i => i.Id.StartsWith("part.plugin."))) item.Enabled = active.ContainsKey(item.Target);
        foreach (var mod in active.Values)
        {
            if (shell.Config.TopDockItems.Any(i => i.Id.StartsWith("part.plugin.") && i.Target == mod.Id)) continue;
            if (shell.Config.TopDockItems.Count >= 100) break;
            shell.Config.TopDockItems.Add(new BarItem { Id = "part.plugin." + Guid.NewGuid().ToString("N"), Kind = "plugin", Target = mod.Id, Name = mod.Name, Group = "right" });
        }
    }
}
