using System;
using System.Collections.Generic;
using System.Linq;

namespace Harbor;

internal static class ShellFeatures
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Settings, Mods> owners = new();
    internal static void Bind(Settings settings, Mods mods) { owners.Remove(settings); owners.Add(settings, mods); }
    internal static readonly (string Component, string Id, string Name, string Description)[] Definitions = {
        ("taskbar", "taskbar", "Taskbar", "Your pinned apps and folders at the bottom of the screen. Optionally include open apps from Taskbar controls."),
        ("top-dock", "minimalists-dock", "Minimalists Dock", "A slim, rearrangeable top Dock. Add only the pieces you want."),
        ("taskbar-folders", "bulky", "Bulky", "Group taskbar apps into named folders with adjustable app sizes."),
        ("media-controls", "now-playing", "Now Playing", "Previous, play/pause and next controls for your music player."),
        ("dock-clock", "dock-clock", "Dock Clock", "Independent date and time pieces with editable formats."),
        ("dock-stats", "dock-meters", "Dock Meters", "Compact CPU and RAM readings in the top Dock."),
        ("active-title", "window-title", "Window Title", "Show the focused app's title as a movable Dock piece."),
        ("network-control", "network-control", "Network Control", "A small Dock button that opens network and quick settings."),
        ("volume-control", "volume-control", "Volume Control", "A small Dock button for audio and quick settings."),
        ("dock-shortcuts", "dock-shortcuts", "Dock Shortcuts", "Optional Files, Mods, notifications and settings buttons in the top Dock."),
        ("desktop-icons", "desktop-shortcuts", "Harbor Home", "A blue 3D desktop emblem that opens an organized app library, with custom categories and a cinematic startup intro."),
        ("file-manager", "files", "Harbor Files", "A file manager with Home, tabs, readable cards and file actions."),
        ("file-preview", "file-preview", "File Preview", "Read images, text and file details inside Harbor Files."),
        ("file-thumbnails", "file-thumbnails", "File Thumbnails", "Small image previews on file cards."),
        ("collection-tabs", "collection-tabs", "Collection Tabs", "Give each Bulky app folder a synchronized tab in Harbor Files."),
        ("taskbar-dividers", "taskbar-dividers", "Taskbar Dividers", "Movable lines and spaces between groups of taskbar apps."),
        ("dock-dividers", "dock-dividers", "Dock Dividers", "Adjustable lines and spaces between top Dock pieces."),
        ("multi-monitor", "multi-monitor", "Multi Monitor", "Show enabled bars on your other monitors too."),
        ("notification-center", "notification-center", "Notification Center", "A place to read and dismiss Harbor and Windows notifications."),
        ("quick-settings", "quick-settings", "Quick Settings", "Volume, app audio and common system controls in a compact panel.")
    };
    internal static readonly HashSet<string> Components = Definitions.Select(d => d.Component).ToHashSet();
    internal static string Id(string component) => "harbor." + Definitions.First(d => d.Component == component).Id;
    internal static bool On(Settings settings, string component) => settings.FeatureVersion < 1 ? Legacy(settings, component) : settings.FeatureComponents.Contains(component);
    internal static bool Legacy(Settings c, string component) => component switch {
        "top-dock" => c.TopDockEnabled, "multi-monitor" => c.AllMonitors, "taskbar-folders" or "collection-tabs" => c.AppFolders.Count > 0,
        "media-controls" => c.ShowMedia, "dock-clock" => c.ShowClock, "dock-stats" => c.ShowStats, "active-title" => c.ShowActiveTitle,
        "network-control" => c.ShowNetwork, "dock-shortcuts" => c.ShowFilesButton || c.ShowModsButton, "file-preview" => c.FilesPreview,
        "file-manager" => c.UseHarborFiles, "taskbar-dividers" => true, "dock-dividers" => c.TopDockItems.Any(i => i.Id.StartsWith("divider.")), _ => true
    };
    internal static void Initialize(Settings settings, Mods mods, bool existingProfile)
    {
        if (settings.FeatureVersion < 1)
        {
            foreach (var definition in Definitions)
            {
                var mod = mods.Catalog.First(m => m.Id == Id(definition.Component)); mods.Install(mod, false);
                if (existingProfile && Legacy(settings, definition.Component) && !settings.EnabledMods.Contains(mod.Id)) settings.EnabledMods.Add(mod.Id);
            }
            settings.FeatureVersion = 1; settings.FeatureSetupPending = !existingProfile; settings.Save();
        }
        Sync(settings, mods);
    }
    internal static void Sync(Settings settings, Mods mods)
    {
        settings.FeatureComponents = mods.Enabled.Where(m => m.Kind == "feature").Select(m => m.Component).ToHashSet();
        settings.FeatureIds = mods.Catalog.Where(m => m.Kind == "feature").GroupBy(m => m.Component).ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToList());
    }
    internal static void Request(Settings settings, string component, bool enabled)
    {
        if (settings.FeatureVersion < 1) return;
        if (enabled)
        {
            if (owners.TryGetValue(settings, out var owner)) { var package = owner.Catalog.First(m => m.Id == Id(component)); if (!owner.Installed(package)) owner.Install(package, false); }
            string id = Id(component); if (!settings.EnabledMods.Contains(id)) settings.EnabledMods.Add(id); settings.FeatureComponents.Add(component);
            ActivatePreferences(settings, component);
            foreach (string dependency in Dependencies(component)) Request(settings, dependency, true);
        }
        else
        {
            var ids = settings.FeatureIds.GetValueOrDefault(component) ?? new List<string> { Id(component) };
            settings.EnabledMods.RemoveAll(ids.Contains); settings.FeatureComponents.Remove(component);
            foreach (string dependent in Definitions.Select(d => d.Component).Where(d => Dependencies(d).Contains(component)).ToArray()) if (settings.FeatureComponents.Contains(dependent)) Request(settings, dependent, false);
        }
    }
    internal static void ActivatePreferences(Settings c, string component)
    {
        switch (component) { case "top-dock": c.TopDockEnabled = true; break; case "multi-monitor": c.AllMonitors = true; break; case "media-controls": c.ShowMedia = true; break; case "dock-clock": c.ShowClock = true; break; case "dock-stats": c.ShowStats = true; break; case "active-title": c.ShowActiveTitle = true; break; case "network-control": c.ShowNetwork = true; break; case "dock-shortcuts": c.ShowFilesButton = c.ShowModsButton = true; break; case "file-manager": c.UseHarborFiles = true; break; case "file-preview": c.FilesPreview = true; break; }
    }
    internal static string[] Dependencies(string component) => component switch {
        "taskbar-folders" or "taskbar-dividers" => new[] { "taskbar" },
        "collection-tabs" => new[] { "taskbar-folders", "file-manager" },
        "file-preview" or "file-thumbnails" => new[] { "file-manager" },
        "network-control" or "volume-control" => new[] { "top-dock", "quick-settings" },
        "media-controls" or "dock-clock" or "dock-stats" or "active-title" or "dock-shortcuts" or "dock-dividers" => new[] { "top-dock" }, _ => Array.Empty<string>()
    };
    internal static List<ModDefinition> Packages() => Definitions.Select(d => new ModDefinition {
        Format = 3, Id = Id(d.Component), Name = d.Name, Kind = "feature", Component = d.Component, Version = "1.4.0", MinimumHarborVersion = "0.10.0",
        Description = d.Description, MainCategory = d.Component.StartsWith("file-") || d.Component is "collection-tabs" or "notification-center" or "quick-settings" ? "Tools" : "Desktop",
        Subcategory = d.Component.StartsWith("file-") || d.Component is "notification-center" or "quick-settings" ? "Utilities" : d.Component == "collection-tabs" ? "Shortcuts" : d.Component is "multi-monitor" or "desktop-icons" ? "Windows" : d.Component is "taskbar" or "taskbar-folders" or "taskbar-dividers" ? "Dock" : "Toolbar",
        Instructions = "Enable " + d.Name + " in Mods. Optional features start disabled on a new profile.\n" + d.Description + "\nUse Customize feature for its controls. Disable it to hide the feature; saved layouts and content are kept.",
        Dependencies = Dependencies(d.Component).Select(component => new ModDependency { Id = Id(component), MinimumVersion = "1.4.0" }).ToList(),
        Tags = new() { "feature", "optional", "shell", "customizable" }
    }).ToList();
    internal static IEnumerable<Pin> VisiblePins(Settings c) => c.Pins.SelectMany(p => !On(c, "taskbar-folders") && TaskbarFolders.Find(c, p.Path) is { } folder ? folder.Apps : new[] { p }.AsEnumerable()).DistinctBy(p => p.Path, StringComparer.OrdinalIgnoreCase);
}
