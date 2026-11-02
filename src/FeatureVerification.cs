using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Harbor;

internal static class FeatureVerification
{
    internal static void Run(Shell shell, Action<string, bool, object?> check)
    {
        var area = new Rect(-1440, -300, 1440, 1000); var box = PluginSnapping.CenterBox(area);
        var four = PluginSnapping.Positions.Skip(1).Take(4).Select(s => PluginSnapping.Place(area, new Size(112, 36), s, .5, .5)).ToArray();
        check("The four default snap points form a widely spaced center box", four.All(area.Contains) && four[1].Left - four[0].Right > area.Width * .4 && four[2].Top - four[0].Bottom > area.Height * .5 && box.Width == area.Width * .6, four);
        check("Every side and screen corner accepts snapping inside the usable area", PluginSnapping.Positions.Skip(5).Where(p => p != "Free").All(p => area.Contains(PluginSnapping.Place(area, new Size(112, 36), p, .3, .7))), null);
        var near = four[0]; near.Offset(10, 12);
        check("Dragging close to a center-box point selects that snap target", PluginSnapping.Nearest(area, near, 36) == PluginSnapping.Positions[1], null);
        var settings = new Settings { TaskbarBaseTransparency = 20, TaskbarHoverTransparency = 70, TaskbarActiveTransparency = 5, TransparencyVersion = 1 };
        check("Taskbar base, hover and right-click have independent transparency", Math.Abs(TaskbarTransparency.Opacity(settings, TaskbarStage.Base) - .8) < .001 && Math.Abs(TaskbarTransparency.Opacity(settings, TaskbarStage.Hover) - .3) < .001 && Math.Abs(TaskbarTransparency.Opacity(settings, TaskbarStage.Activated) - .95) < .001, null);
        settings.TaskbarBaseTransparency = 0; settings.TaskbarHoverTransparency = 100; settings.TaskbarActiveTransparency = double.NaN; settings.BulkAppSize = 200; settings.Normalize();
        check("Transparency accepts half-percent limits and Bulky size is bounded", settings.TaskbarBaseTransparency == .5 && settings.TaskbarHoverTransparency == 99.5 && settings.TaskbarActiveTransparency == .5 && settings.BulkAppSize == 96, null);
        check("Typing keeps the taskbar in its faded stage despite an activation lease", TaskbarTransparency.Stage(true, true, true) == TaskbarStage.Hover && TaskbarTransparency.Stage(false, true, false) == TaskbarStage.Activated, null);
        var divider = new DividerVisual(_ => { }, () => { }); divider.Configure(1, 3, 30, false, 40);
        check("A thin divider still has a usable twenty-pixel drag target", divider.Width >= 20 && divider.Height >= 20, null);
        divider.Configure(80, 4, 50, true, 60);
        check("Divider spacing, thickness and length have independent controls", divider.Width == 80 && divider.Height == 60, null);
        var fresh = new Settings(); var freshMods = new Mods(fresh);
        check("A fresh modular profile enables no optional preset or shell feature", fresh.FeatureVersion == 1 && fresh.FeatureSetupPending && !freshMods.Enabled.Any() && !ShellFeatures.On(fresh, "top-dock") && !ShellFeatures.On(fresh, "taskbar"), null);
        var folder = new AppFolder { Name = "Keep this folder", Apps = new() { new Pin { Name = "Notes", Path = "notepad.exe" }, new Pin { Name = "Calculator", Path = "calc.exe" } } };
        var migrated = new Settings { ModsInitialized = true, AppFolders = new() { folder }, Pins = new() { new Pin { Name = folder.Name, Path = TaskbarFolders.Path(folder) } } };
        migrated.Normalize();
        string pins = JsonSerializer.Serialize(migrated.Pins), folders = JsonSerializer.Serialize(migrated.AppFolders);
        var migratedMods = new Mods(migrated);
        check("Existing shell surfaces migrate into enabled feature plugins", ShellFeatures.On(migrated, "top-dock") && ShellFeatures.On(migrated, "taskbar") && ShellFeatures.On(migrated, "taskbar-folders") && !migrated.FeatureSetupPending, null);
        migratedMods.Enable(migratedMods.Catalog.First(m => m.Id == ShellFeatures.Id("taskbar-folders")), false);
        check("Disabling Bulky shows the apps separately while retaining the folder data", ShellFeatures.VisiblePins(migrated).Count() == 2 && JsonSerializer.Serialize(migrated.Pins) == pins && JsonSerializer.Serialize(migrated.AppFolders) == folders, null);
        migrated.TaskbarDividers.Add(new TaskbarDivider { Before = "notepad.exe" });
        var ungroupedBar = new Dock(new Shell(migrated), shell.CurrentMonitor()); ungroupedBar.Refresh(); ungroupedBar.Refresh();
        check("Dividers remain usable when Bulky displays folder apps separately", ungroupedBar.Width > 0 && migrated.TaskbarDividers.Single().Before == "notepad.exe", null); ungroupedBar.Close();
        migratedMods.Enable(migratedMods.Catalog.First(m => m.Id == ShellFeatures.Id("top-dock")), false);
        check("Disabling Minimalists Dock disables dependent controls without erasing layout", !ShellFeatures.On(migrated, "media-controls") && !ShellFeatures.On(migrated, "dock-clock") && migrated.TopDockItems.Count > 0, null);
        var nowPlaying = freshMods.Catalog.First(m => m.Id == ShellFeatures.Id("media-controls")); freshMods.Enable(nowPlaying, true);
        check("Enabling a feature installs its declared Dock dependency", ShellFeatures.On(fresh, "media-controls") && ShellFeatures.On(fresh, "top-dock") && !ShellFeatures.On(fresh, "taskbar-folders"), null);
        check("Requested module names are real installable feature manifests", freshMods.Catalog.Any(m => m.Kind == "feature" && m.Name == "Bulky") && freshMods.Catalog.Any(m => m.Kind == "feature" && m.Name == "Minimalists Dock") && freshMods.Catalog.Count(m => m.Kind == "feature") == 20, null);
        string options = JsonSerializer.Serialize(shell.Config.ModOptions);
        try
        {
            var system = shell.Mods.Catalog.First(m => m.Id == "harbor.system"); shell.Mods.Set(system, "meter-mode", "CPU + RAM", false);
            string both = PluginPresentation.Reading(shell, system, true);
            shell.Mods.Set(system, "meter-mode", "RAM", false); string ram = PluginPresentation.Reading(shell, system, true);
            check("CPU and RAM can share one compact control or be shown separately", both.Contains("CPU") && both.Contains("RAM") && ram.Contains("RAM") && !ram.Contains("CPU"), null);
            shell.Mods.Set(system, "compact-format", "CPU {cpu:0}% / RAM {ram:0}%", false);
            check("Custom compact formats combine live data tokens", PluginPresentation.Reading(shell, system, true).Contains("CPU") && !PluginPresentation.Reading(shell, system, true).Contains('{'), null);
            var clock = shell.Mods.Catalog.First(m => m.Id == "harbor.clock"); shell.Mods.Set(clock, "format", "HH:mm:ss", false);
            check("Clock format changes affect the compact control", PluginPresentation.Reading(shell, clock, true) == "08:25:00", null);
            var drive = shell.Mods.Catalog.First(m => m.Id == "harbor.storage"); shell.Mods.Set(drive, "drive-mode", "All drives", false);
            check("Several selected drives can appear in one control", PluginPresentation.Reading(shell, drive, true).Contains("C:") && PluginPresentation.Reading(shell, drive, true).Contains("D:"), null);
            var note = shell.Mods.Catalog.First(m => m.Id == "harbor.notes"); shell.Mods.Set(note, "always-on", "Yes", false);
            var panel = new PluginPopup(shell, note, new Widget(shell, note, inline: true), shell.CurrentMonitor());
            check("Always-on is restored from the plugin's saved settings", panel.Pinned, null); panel.UserClose();
            check("Closing an always-on panel turns the saved mode off", shell.Mods.Value(note, "always-on") == "No", null);
        }
        finally { shell.Config.ModOptions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(options)!; shell.Config.Save(); }
    }
}
