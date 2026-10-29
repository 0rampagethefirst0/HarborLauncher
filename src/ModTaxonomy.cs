using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Harbor;

internal static class ModTaxonomy
{
    internal static readonly string[] Main = { "Desktop", "Themes", "Tools" };
    internal static readonly Dictionary<string, string[]> Children = new()
    {
        ["Desktop"] = new[] { "Windows", "Dock", "Toolbar", "Dashboards" },
        ["Themes"] = new[] { "Dark", "Light", "Colorful", "Minimal" },
        ["Tools"] = new[] { "Productivity", "Monitoring", "Time", "Audio", "Utilities", "Shortcuts" }
    };
    internal static readonly string[] Tags = {
        "animated", "minimal", "dark", "light", "colorful", "pastel", "compact", "rounded", "flat", "high-contrast",
        "keyboard", "mouse", "shortcuts", "multi-monitor", "per-app", "gaming", "streaming", "accessibility",
        "windows", "dock", "toolbar", "desktop", "widgets", "dashboards", "layout", "transparency", "always-on-top",
        "position", "memory", "labels", "scroll", "audio", "volume", "media", "productivity", "focus", "notes",
        "tasks", "clipboard", "folders", "launcher", "calculator", "converter", "time", "clock", "calendar",
        "timer", "stopwatch", "time-zones", "monitoring", "cpu", "ram", "network", "storage", "uptime", "native",
        "customizable", "offline", "low-gpu", "automation", "organization", "work", "study", "creative", "utility"
    };
    internal static string MainOf(ModDefinition m) => Main.Contains(m.MainCategory) ? m.MainCategory :
        m.Kind == "theme" ? "Themes" : m.Kind == "tweak" || m.Component == "layouts" || m.Id.EndsWith("window-desk") ? "Desktop" : "Tools";
    internal static string SubOf(ModDefinition m)
    {
        string main = MainOf(m);
        if (Children[main].Contains(m.Subcategory)) return m.Subcategory;
        if (main == "Themes") return m.Id.EndsWith("forest") || m.Id.EndsWith("ember") || m.Id.EndsWith("plum") ? "Colorful" : "Dark";
        if (main == "Desktop") return m.Component.StartsWith("dock-") ? "Dock" : m.Component.StartsWith("toolbar-") || m.Component is "clock-seconds" or "title-actions" ? "Toolbar" : m.Component == "dashboard" ? "Dashboards" : "Windows";
        return m.Component switch {
            "clock" or "world-clock" or "stopwatch" => "Time",
            "system" or "network" or "storage" or "uptime" => "Monitoring",
            "launchpad" or "folders" => "Shortcuts",
            "calculator" or "converter" => "Utilities",
            _ => m.Id.EndsWith("sound-desk") ? "Audio" : m.Id.EndsWith("resource-desk") ? "Monitoring" : "Productivity"
        };
    }
    internal static string Description(string main) => main switch {
        "Desktop" => "Make every window, dock and desktop work your way.",
        "Themes" => "Change the colors used across Harbor.",
        _ => "Add notes, timers, system meters, and shortcuts to your desktop."
    };
    internal static void Enrich(ModDefinition m)
    {
        m.MainCategory = MainOf(m); m.Subcategory = SubOf(m);
        var tags = new List<string> { m.MainCategory.ToLowerInvariant(), m.Subcategory.ToLowerInvariant(), "native", "customizable", "offline", "low-gpu" };
        if (m.Kind == "widget") tags.AddRange(new[] { "widgets", "multi-monitor" });
        if (m.Kind == "theme") tags.AddRange(new[] { "dark", "rounded", "colorful" });
        tags.AddRange(m.Component.Split('-'));
        if (m.Component.StartsWith("window-")) tags.Add("per-app");
        if (m.Component == "system") tags.AddRange(new[] { "cpu", "ram", "monitoring" });
        if (m.Component is "notes" or "tasks" or "focus") tags.AddRange(new[] { "work", "study", "productivity" });
        m.Tags = m.Tags.Concat(tags).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();
    }
    internal static void Validate(ModDefinition m)
    {
        if (m.MainCategory == null || m.Subcategory == null || m.PreviewImage == null || m.PreviewAccent == null) throw new InvalidDataException("Invalid category or preview metadata.");
        if (m.MainCategory.Length > 0 && !Main.Contains(m.MainCategory)) throw new InvalidDataException("Choose one main category: Desktop, Themes or Tools.");
        if (m.Subcategory.Length > 0 && !Children[MainOf(m)].Contains(m.Subcategory)) throw new InvalidDataException("Choose a subcategory belonging to " + MainOf(m) + ".");
        if (m.PreviewAccent.Length > 0 && !Regex.IsMatch(m.PreviewAccent, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("Preview accent must be a #RRGGBB color.");
        if (m.PreviewImage.Length == 0) return;
        if (m.PreviewImage.Length > 700_000 || !(m.PreviewImage.StartsWith("data:image/png;base64,") || m.PreviewImage.StartsWith("data:image/jpeg;base64,"))) throw new InvalidDataException("Banner images must be embedded PNG or JPEG files under 500 KB.");
        byte[] image;
        try { image = Convert.FromBase64String(m.PreviewImage[(m.PreviewImage.IndexOf(',') + 1)..]); }
        catch (FormatException) { throw new InvalidDataException("The banner image has invalid base64 data."); }
        bool png = image.Length >= 24 && image[0] == 137 && image[1] == 80 && image[2] == 78 && image[3] == 71;
        bool jpg = image.Length >= 4 && image[0] == 255 && image[1] == 216 && image[^2] == 255 && image[^1] == 217;
        if (image.Length > 500_000 || !(png || jpg)) throw new InvalidDataException("The banner is not a supported image.");
        try {
            using var stream = new MemoryStream(image);
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.None);
            var frame = decoder.Frames[0]; if (frame.PixelWidth > 4096 || frame.PixelHeight > 4096) throw new InvalidDataException("Banner dimensions must be at most 4096 pixels.");
        } catch (Exception ex) when (ex is not InvalidDataException) { throw new InvalidDataException("The banner image cannot be decoded.", ex); }
    }
}
