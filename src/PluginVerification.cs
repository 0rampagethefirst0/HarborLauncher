using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Harbor;

internal static class PluginVerification
{
    internal static void Run(Shell model, Action<string, bool, object?> check)
    {
        var tools = model.Mods.Catalog.Where(m => m.Id.StartsWith("harbor.") && m.Kind == "widget").ToArray();
        check("All twenty preset tools offer independent compact access with Dock optional", tools.Length == 20 && tools.All(m => m.Options.First(o => o.Key == "access").Default == PluginPresentation.Edge && PluginPresentation.Modes.All(m.Options.First(o => o.Key == "access").Choices.Contains)), null);
        check("Preset tool guides explain expansion and collapse instead of requiring Show Desktop", tools.All(m => ModHelp.Steps(m)[0].Contains("Click it to expand") && !ModHelp.Steps(m)[0].Contains("Win + D")), null);
        var legacy = new ModDefinition { Id = "local.old-note", Component = "notes", Author = "Local" };
        check("Older custom panels retain their desktop presentation", PluginPresentation.Mode(model.Mods, legacy) == PluginPresentation.Desktop, null);
        var area = new Rect(-1440, -500, 1440, 1000);
        var bounds = PluginPresentation.Place(area, new Size(112, 36), PluginPresentation.Floating, "Right", -100, 100);
        check("Movable controls remain inside a negative-origin monitor", area.Contains(bounds), bounds);
        var invalid = PluginPresentation.Place(area, new Size(112, 36), PluginPresentation.Floating, "Right", double.NaN, double.PositiveInfinity);
        check("Invalid control coordinates fall back to a visible screen position", area.Contains(invalid), invalid);
        var left = PluginPresentation.Place(area, new Size(112, 36), PluginPresentation.Edge, "Left", .9, .5); var right = PluginPresentation.Place(area, new Size(112, 36), PluginPresentation.Edge, "Right", .1, .5);
        check("Edge controls anchor to the selected side independently of desktop positions", left.Left == area.Left + 6 && right.Right == area.Right - 6 && left.Top == right.Top, null);
        var packed = PluginPresentation.PackVertical(new[] { new Rect(-118, 0, 112, 36), new Rect(-118, 0, 112, 36), new Rect(-118, 0, 112, 36) }, area, 8);
        check("Dragging edge controls together reflows them without overlapping", packed.All(area.Contains) && packed[0].Bottom + 8 <= packed[1].Top && packed[1].Bottom + 8 <= packed[2].Top, null);
        string savedEnabled = JsonSerializer.Serialize(model.Config.EnabledMods), savedPieces = JsonSerializer.Serialize(model.Config.TopDockItems), savedOptions = JsonSerializer.Serialize(model.Config.ModOptions);
        try
        {
            model.Config.EnabledMods = tools.Select(m => m.Id).ToList();
            foreach (var tool in tools) { model.Mods.Set(tool, "access", PluginPresentation.Edge, false); model.Mods.Install(tool, false); }
            model.Plugins.Rebuild();
            check("Collapsed tools keep their runtime without creating visible test windows", model.Plugins.BackgroundCount == 20 && Application.Current.Windows.OfType<PluginHandle>().All(w => !w.IsVisible), null);
            check("Compact presets leave the desktop free of their old full panels", !model.Mods.Enabled.Any(m => PluginPresentation.IsDesktop(model.Mods, m)), null);
            check("Default compact access does not add anything to the top Dock", JsonSerializer.Serialize(model.Config.TopDockItems) == savedPieces, null);
            var folders = tools.First(m => m.Id == "harbor.folders");
            model.Mods.Set(folders, "access", PluginPresentation.Dock, false); model.Plugins.Rebuild(); model.Plugins.Rebuild();
            var dockItems = model.Config.TopDockItems.Where(i => i.Id.StartsWith("part.plugin.") && i.Target == folders.Id).ToArray();
            check("Opting into Dock access creates one independently movable plugin piece", dockItems.Length == 1 && dockItems[0].Enabled && dockItems[0].Kind == "plugin", null);
            BarLayout.Normalize(model.Config); var duplicate = BarLayout.Duplicate(model.Config, dockItems[0]); BarLayout.Normalize(model.Config);
            check("Plugin Dock pieces retain their target through duplicate and layout normalization", duplicate.Target == folders.Id && model.Config.TopDockItems.Contains(duplicate), null);
            model.Mods.Set(folders, "access", PluginPresentation.Floating, false); model.Plugins.Rebuild();
            check("Changing independent access retires only the managed Dock entry", !dockItems[0].Enabled && duplicate.Enabled, null);
            model.Mods.Set(folders, "access", PluginPresentation.Desktop, false);
            check("A full desktop panel is an explicit per-plugin option", PluginPresentation.IsDesktop(model.Mods, folders) && !PluginPresentation.IsDesktop(model.Mods, tools.First(m => m.Id == "harbor.clock")), null);
            var focus = tools.First(m => m.Id == "harbor.focus");
            var timer = (TimerState)model.ModRuntime[focus.Id]; timer.Seconds = .01; timer.Watch.Start(); Thread.Sleep(30); model.Plugins.Tick();
            check("A collapsed focus timer completes and records a notification", timer.Finished && model.Notifications.Items.Any(n => n.Title == "Focus timer finished"), null);
            int notices = model.Notifications.Items.Count(n => n.Title == "Focus timer finished"); model.Plugins.Tick();
            check("Collapsed timer completion is reported once", model.Notifications.Items.Count(n => n.Title == "Focus timer finished") == notices, null);
            var note = tools.First(m => m.Id == "harbor.notes"); model.Mods.Set(note, "content", "Original note", false);
            var untouched = new Widget(model, note, inline: true); var edited = new Widget(model, note, inline: true);
            Descendants<TextBox>(edited.View).First().Text = "Saved immediately while collapsing"; edited.Flush(); untouched.Flush();
            check("Collapsing flushes note edits without an untouched view overwriting them", model.Mods.Value(note, "content") == "Saved immediately while collapsing", null);
            var popup = new PluginPopup(model, note, edited, model.CurrentMonitor()); int closing = 0; popup.Closing += (_, _) => { closing++; popup.RequestClose(); }; popup.RequestClose(); popup.RequestClose();
            check("Tool popups tolerate nested close events without crashing the shell", closing == 1, null);
            check("Compact controls use descriptive names rather than two-letter abbreviations", tools.All(m => PluginPresentation.ShortName(m).Length >= 3 && PluginPresentation.Icon(m).Length > 0), null);
        }
        finally
        {
            model.Plugins.Dispose(); model.Config.EnabledMods = JsonSerializer.Deserialize<List<string>>(savedEnabled)!;
            model.Config.TopDockItems = JsonSerializer.Deserialize<List<BarItem>>(savedPieces)!; model.Config.ModOptions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(savedOptions)!;
            model.Config.Save();
        }
    }
    static IEnumerable<T> Descendants<T>(DependencyObject item) where T : DependencyObject
    {
        if (item is T match) yield return match;
        foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) foreach (var nested in Descendants<T>(child)) yield return nested;
    }
}
