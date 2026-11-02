using System;
using System.Collections.Generic;
using System.Linq;

namespace Harbor;

public sealed class AppUse
{
    public int Count { get; set; }
    public DateTime LastUsedUtc { get; set; }
}

internal static class HomeLayout
{
    internal static readonly string[] PreferenceKeys = { "DesktopRecycleBin", "HomeStartTab", "HomeColumns", "HomeRows", "HomeIconSize", "HomeTextSize", "HomeSidebarWidth", "HomeWidth", "HomeHeight", "HomeCornerRadius", "HomeShowLabels", "HomeShowKinds", "HomeShowCounts", "HomeTabOrder", "HomeHiddenTabs", "HomeTabNames", "DesktopCustomCategories" };
    internal static void CopyPreferences(Settings from, Settings to) { foreach (string key in PreferenceKeys) { var property = typeof(Settings).GetProperty(key)!; property.SetValue(to, property.GetValue(from)); } Normalize(to); }
    internal static IEnumerable<string> Tabs(Settings c) => new[] { "All apps", "All", "Favorites" }.Concat(DesktopLibraryModel.Groups(c));
    internal static IEnumerable<string> OrderedTabs(Settings c) => c.HomeTabOrder.Concat(Tabs(c)).Where(Tabs(c).Contains).Distinct(StringComparer.OrdinalIgnoreCase);
    internal static string Label(Settings c, string tab) => c.HomeTabNames.GetValueOrDefault(tab, tab == "All" ? "All items" : tab == "Desktop" ? "Home" : tab);
    internal static void Normalize(Settings c)
    {
        c.HomeColumns = Math.Clamp(c.HomeColumns, 2, 6); c.HomeRows = Math.Clamp(c.HomeRows, 1, 5);
        c.HomeIconSize = Math.Clamp(c.HomeIconSize, 24, 80); c.HomeTextSize = Math.Clamp(c.HomeTextSize, 12, 22);
        c.HomeSidebarWidth = Math.Clamp(c.HomeSidebarWidth, 160, 300); c.HomeWidth = Math.Clamp(c.HomeWidth, 880, 1600); c.HomeHeight = Math.Clamp(c.HomeHeight, 640, 1100); c.HomeCornerRadius = Math.Clamp(c.HomeCornerRadius, 0, 36);
        if (c.HomeStartTab is not "Desktop" and not "Frequently used" and not "All apps") c.HomeStartTab = "Frequently used";
        c.HomeTabOrder = (c.HomeTabOrder ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(120).ToList();
        c.HomeHiddenTabs = (c.HomeHiddenTabs ?? new()).Where(s => !string.IsNullOrWhiteSpace(s) && s is not "Desktop" and not "Frequently used").Distinct(StringComparer.OrdinalIgnoreCase).Take(120).ToList();
        c.HomeTabNames = (c.HomeTabNames ?? new()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).Take(120).ToDictionary(p => p.Key, p => p.Value.Trim()[..Math.Min(36, p.Value.Trim().Length)]);
        c.AppUsage = (c.AppUsage ?? new()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value != null).OrderByDescending(p => p.Value.LastUsedUtc).Take(5000).ToDictionary(p => p.Key, p => new AppUse { Count = Math.Clamp(p.Value.Count, 0, 1000000), LastUsedUtc = p.Value.LastUsedUtc }, StringComparer.OrdinalIgnoreCase);
    }
    internal static void Record(Settings c, string path)
    {
        if (!c.AppUsage.TryGetValue(path, out var use)) c.AppUsage[path] = use = new();
        use.Count = Math.Min(1000000, use.Count + 1); use.LastUsedUtc = DateTime.UtcNow; c.Save();
    }
    internal static List<DesktopLibraryItem> Frequent(Settings c, IEnumerable<DesktopLibraryItem> items)
    {
        var pinned = TaskbarFolders.AllApps(c).Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = items.DistinctBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList();
        var chosen = all.Where(i => c.AppUsage.ContainsKey(i.Path) || pinned.Contains(i.Path)).ToList();
        if (chosen.Count == 0) chosen = all.Where(i => i.App).Take(12).ToList();
        return chosen
            .OrderByDescending(i => c.AppUsage.GetValueOrDefault(i.Path)?.Count ?? 0).ThenByDescending(i => c.AppUsage.GetValueOrDefault(i.Path)?.LastUsedUtc ?? DateTime.MinValue).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
