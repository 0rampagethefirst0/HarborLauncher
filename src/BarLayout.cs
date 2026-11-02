using System;
using System.Collections.Generic;
using System.Linq;

namespace Harbor;

public sealed class BarItem
{
    public string Id { get; set; } = "";
    public string Group { get; set; } = "right";
    public bool Enabled { get; set; } = true;
    public bool Space { get; set; }
    public double? Position { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public string Format { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string Target { get; set; } = "";
    public int Width { get; set; }
    public int FontSize { get; set; }
    public double DividerThickness { get; set; } = 1;
    public double DividerLength { get; set; } = 18;
}
public sealed class TaskbarDivider
{
    public bool Enabled { get; set; } = true;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
    public bool Space { get; set; }
    public double Width { get; set; } = 24;
    public double Thickness { get; set; } = 1;
    public double Length { get; set; } = 26;
}
internal static class BarLayout
{
    internal const string DragFormat = "Harbor.TopDockItem";
    internal const string DividerDragFormat = "Harbor.TaskbarDivider";
    internal static readonly Dictionary<string, string> Labels = new() {
        ["apps"] = "Apps", ["title"] = "Active window", ["clock"] = "Clock", ["stats"] = "CPU and memory",
        ["previous"] = "Previous track", ["play"] = "Play / pause", ["next"] = "Next track", ["network"] = "Network",
        ["volume"] = "Volume", ["files"] = "Files", ["mods"] = "Mods", ["notifications"] = "Notifications", ["settings"] = "Settings",
        ["date"] = "Date", ["time"] = "Time", ["cpu"] = "CPU", ["memory"] = "Memory", ["text"] = "Custom text", ["app"] = "App shortcut", ["plugin"] = "Plugin"
    };
    internal static string Component(BarItem item) => item.Kind.Length > 0 ? item.Kind : item.Id;
    internal static string Label(BarItem item) => item.Name.Length > 0 ? item.Name : item.Id.StartsWith("divider.") ? item.Space ? "Space" : "Divider" : Labels.GetValueOrDefault(Component(item), item.Id);
    internal static int ReservedTop(Settings c, bool primary) => ShellFeatures.On(c, "top-dock") && c.TopDockEnabled && c.TopDockReserveSpace && (!c.TopDockPrimaryOnly || primary) ? c.TopBarHeight : 0;
    internal static string? Feature(string kind) => kind switch { "title" => "active-title", "clock" or "date" or "time" => "dock-clock", "stats" or "cpu" or "memory" => "dock-stats", "previous" or "play" or "next" => "media-controls", "network" => "network-control", "volume" => "volume-control", "files" or "mods" or "notifications" or "settings" => "dock-shortcuts", _ => null };
    internal static bool Legacy(Settings c, string id) => (Feature(id) is not string feature || ShellFeatures.On(c, feature)) && (id switch { "title" => c.ShowActiveTitle, "clock" or "date" or "time" => c.ShowClock, "stats" or "cpu" or "memory" => c.ShowStats, "previous" or "play" or "next" => c.ShowMedia, "network" => c.ShowNetwork, "files" => c.ShowFilesButton, "mods" => c.ShowModsButton, _ => true });
    internal static List<BarItem> Defaults(Settings c)
    {
        var list = Labels.Where(p => p.Key is not "clock" and not "cpu" and not "memory" and not "text" and not "app" and not "plugin").Select(p => new BarItem { Id = p.Key, Group = p.Key is "apps" or "title" ? "left" : p.Key is "date" or "time" ? "center" : "right", Enabled = Legacy(c, p.Key) }).ToList();
        list.Add(new BarItem { Id = "clock", Group = "center", Enabled = false });
        list.Insert(1, new BarItem { Id = "divider." + Guid.NewGuid().ToString("N"), Group = "left" }); return list;
    }
    internal static void Normalize(Settings c)
    {
        c.TopDockItems ??= new(); c.TaskbarDividers ??= new();
        if (c.TopDockItems.Count == 0) c.TopDockItems = Defaults(c);
        var seen = new HashSet<string>(); c.TopDockItems = c.TopDockItems.Where(i => i != null && i.Id != null && (Labels.ContainsKey(i.Id) || (i.Id.StartsWith("part.") && i.Id.Length < 60 && Labels.ContainsKey(i.Kind ?? "")) || (i.Id.StartsWith("divider.") && i.Id.Length < 60)) && seen.Add(i.Id)).Take(100).ToList();
        foreach (var item in c.TopDockItems) {
            item.Kind ??= ""; item.Name ??= ""; item.Text ??= ""; item.Format ??= ""; item.TimeZone ??= ""; item.Target ??= "";
            item.Name = item.Name[..Math.Min(60, item.Name.Length)]; item.Text = item.Text[..Math.Min(200, item.Text.Length)]; item.Format = item.Format[..Math.Min(100, item.Format.Length)]; item.TimeZone = item.TimeZone[..Math.Min(150, item.TimeZone.Length)]; item.Target = item.Target[..Math.Min(4000, item.Target.Length)];
            item.Width = Math.Clamp(item.Width, 0, 600); item.FontSize = item.FontSize == 0 ? 0 : Math.Clamp(item.FontSize, 8, 36);
            item.DividerThickness = double.IsFinite(item.DividerThickness) ? Math.Clamp(item.DividerThickness, 1, 6) : 1; item.DividerLength = double.IsFinite(item.DividerLength) ? Math.Clamp(item.DividerLength, 6, 64) : 18;
            if (item.Group is not "left" and not "center" and not "right") item.Group = "right";
            if (item.Position is double position) item.Position = double.IsFinite(position) ? Math.Clamp(position, 0, 1) : null;
        }
        if (c.TopDockLayoutVersion < 2) {
            var combined = c.TopDockItems.FirstOrDefault(i => i.Id == "clock");
            if (combined != null && !c.TopDockItems.Any(i => i.Id is "date" or "time")) {
                int index = c.TopDockItems.IndexOf(combined); bool enabled = combined.Enabled && c.ShowClock; combined.Enabled = false;
                c.TopDockItems.Insert(index++, new BarItem { Id = "date", Group = combined.Group, Enabled = enabled, Position = combined.Position is double p ? Math.Clamp(p - .035, 0, 1) : null });
                c.TopDockItems.Insert(index, new BarItem { Id = "time", Group = combined.Group, Enabled = enabled, Position = combined.Position is double t ? Math.Clamp(t + .035, 0, 1) : null });
            }
            c.TopDockLayoutVersion = 2;
        }
        foreach (string id in Labels.Keys.Where(id => id is not "cpu" and not "memory" and not "text" and not "app" and not "plugin" && c.TopDockItems.All(i => i.Id != id))) c.TopDockItems.Add(new BarItem { Id = id, Enabled = false });
        seen.Clear(); c.TaskbarDividers = c.TaskbarDividers.Where(d => d != null && !string.IsNullOrEmpty(d.Id) && d.Id.Length < 60 && d.Before != null && d.After != null && seen.Add(d.Id)).Take(30).ToList();
    }
    internal static void Enable(Settings c, BarItem item, bool enabled)
    {
        if (item.Id.StartsWith("part.plugin.") && !enabled) { if (!c.ModOptions.TryGetValue(item.Target, out var values)) c.ModOptions[item.Target] = values = new(); values["access"] = PluginPresentation.Edge; }
        item.Enabled = enabled; if (!enabled) return;
        if (Feature(Component(item)) is string feature) ShellFeatures.Request(c, feature, true);
        switch (Component(item)) { case "title": c.ShowActiveTitle = true; break; case "clock": case "date": case "time": c.ShowClock = true; break; case "stats": case "cpu": case "memory": c.ShowStats = true; break; case "previous": case "play": case "next": c.ShowMedia = true; break; case "network": c.ShowNetwork = true; break; case "files": c.ShowFilesButton = true; break; case "mods": c.ShowModsButton = true; break; }
    }
    internal static void Move(Settings c, string id, string? before, string group)
    {
        if (group is not "left" and not "center" and not "right") return;
        var item = c.TopDockItems.FirstOrDefault(i => i.Id == id); if (item == null || id == before) return;
        c.TopDockItems.Remove(item); item.Group = group; item.Position = null; int index = before == null ? -1 : c.TopDockItems.FindIndex(i => i.Id == before);
        if (index >= 0 && c.TopDockItems[index].Position is double anchor) item.Position = Math.Max(0, anchor - .000001);
        if (index < 0) { index = c.TopDockItems.FindLastIndex(i => i.Group == group) + 1; if (index == 0) index = c.TopDockItems.Count; }
        c.TopDockItems.Insert(index, item);
    }
    internal static BarItem AddDivider(Settings c, string group, string? before = null, bool space = false)
    {
        ShellFeatures.Request(c, "dock-dividers", true);
        var item = new BarItem { Id = "divider." + Guid.NewGuid().ToString("N"), Group = group, Space = space }; c.TopDockItems.Add(item); Move(c, item.Id, before, group); return item;
    }
    internal static BarItem Duplicate(Settings c, BarItem source)
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<BarItem>(System.Text.Json.JsonSerializer.Serialize(source))!;
        copy.Id = source.Id.StartsWith("divider.") ? "divider." + Guid.NewGuid().ToString("N") : "part." + Guid.NewGuid().ToString("N");
        if (!copy.Id.StartsWith("divider.")) copy.Kind = Component(source);
        copy.Position = source.Position.HasValue ? Math.Clamp(source.Position.Value + .03, 0, 1) : null;
        c.TopDockItems.Insert(c.TopDockItems.IndexOf(source) + 1, copy); return copy;
    }
}
