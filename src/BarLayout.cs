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
}
public sealed class TaskbarDivider
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
    public bool Space { get; set; }
}
internal static class BarLayout
{
    internal static readonly Dictionary<string, string> Labels = new() {
        ["apps"] = "Apps", ["title"] = "Active window", ["clock"] = "Clock", ["stats"] = "CPU and memory",
        ["previous"] = "Previous track", ["play"] = "Play / pause", ["next"] = "Next track", ["network"] = "Network",
        ["volume"] = "Volume", ["files"] = "Files", ["mods"] = "Mods", ["notifications"] = "Notifications", ["settings"] = "Settings"
    };
    internal static string Label(BarItem item) => item.Id.StartsWith("divider.") ? item.Space ? "Space" : "Divider" : Labels.GetValueOrDefault(item.Id, item.Id);
    internal static int ReservedTop(Settings c, bool primary) => c.TopDockEnabled && c.TopDockReserveSpace && (!c.TopDockPrimaryOnly || primary) ? c.TopBarHeight : 0;
    internal static bool Legacy(Settings c, string id) => id switch { "title" => c.ShowActiveTitle, "clock" => c.ShowClock, "stats" => c.ShowStats, "previous" or "play" or "next" => c.ShowMedia, "network" => c.ShowNetwork, "files" => c.ShowFilesButton, "mods" => c.ShowModsButton, _ => true };
    internal static List<BarItem> Defaults(Settings c)
    {
        var list = Labels.Select(p => new BarItem { Id = p.Key, Group = p.Key is "apps" or "title" ? "left" : p.Key == "clock" ? "center" : "right", Enabled = Legacy(c, p.Key) }).ToList();
        list.Insert(1, new BarItem { Id = "divider." + Guid.NewGuid().ToString("N"), Group = "left" }); return list;
    }
    internal static void Normalize(Settings c)
    {
        c.TopDockItems ??= new(); c.TaskbarDividers ??= new();
        if (c.TopDockItems.Count == 0) c.TopDockItems = Defaults(c);
        var seen = new HashSet<string>(); c.TopDockItems = c.TopDockItems.Where(i => i != null && i.Id != null && (Labels.ContainsKey(i.Id) || (i.Id.StartsWith("divider.") && i.Id.Length < 60)) && seen.Add(i.Id)).Take(60).ToList();
        foreach (var item in c.TopDockItems) if (item.Group is not "left" and not "center" and not "right") item.Group = "right";
        foreach (string id in Labels.Keys.Where(id => c.TopDockItems.All(i => i.Id != id))) c.TopDockItems.Add(new BarItem { Id = id, Enabled = false });
        seen.Clear(); c.TaskbarDividers = c.TaskbarDividers.Where(d => d != null && !string.IsNullOrEmpty(d.Id) && d.Id.Length < 60 && d.Before != null && d.After != null && seen.Add(d.Id)).Take(30).ToList();
    }
    internal static void Enable(Settings c, BarItem item, bool enabled)
    {
        item.Enabled = enabled; if (!enabled) return;
        switch (item.Id) { case "title": c.ShowActiveTitle = true; break; case "clock": c.ShowClock = true; break; case "stats": c.ShowStats = true; break; case "previous": case "play": case "next": c.ShowMedia = true; break; case "network": c.ShowNetwork = true; break; case "files": c.ShowFilesButton = true; break; case "mods": c.ShowModsButton = true; break; }
    }
    internal static void Move(Settings c, string id, string? before, string group)
    {
        if (group is not "left" and not "center" and not "right") return;
        var item = c.TopDockItems.FirstOrDefault(i => i.Id == id); if (item == null || id == before) return;
        c.TopDockItems.Remove(item); item.Group = group; int index = before == null ? -1 : c.TopDockItems.FindIndex(i => i.Id == before);
        if (index < 0) { index = c.TopDockItems.FindLastIndex(i => i.Group == group) + 1; if (index == 0) index = c.TopDockItems.Count; }
        c.TopDockItems.Insert(index, item);
    }
    internal static BarItem AddDivider(Settings c, string group, string? before = null, bool space = false)
    {
        var item = new BarItem { Id = "divider." + Guid.NewGuid().ToString("N"), Group = group, Space = space }; c.TopDockItems.Add(item); Move(c, item.Id, before, group); return item;
    }
}
