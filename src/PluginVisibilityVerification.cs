using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Harbor;

internal static class PluginVisibilityVerification
{
    internal static void Run(Shell model, Action<string, bool, object?> check)
    {
        var discord = new WindowEntry(new IntPtr(501), "Chat", @"C:\Apps\Discord\app-1.2.3\Discord.exe", "Discord", IntPtr.Zero);
        var opera = new WindowEntry(new IntPtr(502), "Browser", @"C:\Apps\Opera\opera.exe", "opera", IntPtr.Zero);
        var packaged = new WindowEntry(new IntPtr(503), "Music", "", "ApplicationFrameHost", IntPtr.Zero, "SpotifyAB.SpotifyMusic_test!Spotify");
        check("Apps-only tools follow opening and closing the Apps screen", !PluginVisibility.Evaluate(PluginVisibility.Apps, false, Array.Empty<string>(), new[] { discord }) && PluginVisibility.Evaluate(PluginVisibility.Apps, true, Array.Empty<string>(), Array.Empty<WindowEntry>()), null);
        check("An app rule stays active while any selected app has a window", PluginVisibility.Evaluate(PluginVisibility.Selected, false, new[] { "Discord.exe", "opera.exe" }, new[] { opera }) && !PluginVisibility.Evaluate(PluginVisibility.Selected, true, new[] { "Discord.exe", "opera.exe" }, Array.Empty<WindowEntry>()), null);
        check("An empty chosen-app list never exposes a tool accidentally", !PluginVisibility.Evaluate(PluginVisibility.Selected, true, Array.Empty<string>(), new[] { discord }), null);
        check("Always available does not depend on Apps or other windows", PluginVisibility.Evaluate(PluginVisibility.Always, false, Array.Empty<string>(), Array.Empty<WindowEntry>()), null);
        check("App rules match executable names, paths and packaged identities", PluginVisibility.Matches("discord.EXE", discord) && PluginVisibility.Matches(discord.Path, discord) && PluginVisibility.Matches(@"shell:AppsFolder\SpotifyAB.SpotifyMusic_test!Spotify", packaged), null);
        check("Several app rules can include a match after more than one hundred selections", PluginVisibility.Evaluate(PluginVisibility.Selected, false, Enumerable.Range(0, 150).Select(i => "example" + i + ".exe").Append("Discord.exe"), new[] { discord }), null);
        check("App wildcard rules are anchored and do not match window titles", PluginVisibility.Matches("Disc*.exe", discord) && !PluginVisibility.Matches("Chat", discord) && !PluginVisibility.Matches("cord.exe", discord), null);
        check("The app picker stores update-resistant executable identities", PluginVisibility.AppKey(new AppEntry { Name = "Discord", Target = @"C:\Apps\Discord\Update.exe" }) == "Discord.exe" && PluginVisibility.AppKey(new AppEntry { Name = "Opera GX", Target = opera.Path }) == "opera.exe", null);
        check("Document shortcuts are named after their app in the app picker", PluginVisibility.AppName("Game configuration", "notepad.exe") == "notepad" && PluginVisibility.AppName("Opera GX", "opera.exe") == "Opera GX", null);
        var tools = model.Mods.Catalog.Where(m => m.Kind == "widget" && m.Author == "Harbor").ToArray();
        check("Preset tools default to Apps-only visibility and explain all three modes", tools.Length == 20 && tools.All(m => m.Options.First(o => o.Key == "visibility").Default == PluginVisibility.Apps && ModHelp.Steps(m).Any(s => s.Contains("With chosen apps"))), null);
        check("Older custom plugins retain always-available visibility", PluginVisibility.Mode(model.Mods, new ModDefinition { Id = "local.legacy", Component = "notes", Author = "Local" }) == PluginVisibility.Always, null);
        var layoutConfig = new Settings { TopDockItems = new() { new BarItem { Id = "part.visible", Position = .3 }, new BarItem { Id = "part.hidden", Position = .7 } } };
        var placement = new DockPlacementPanel(layoutConfig); placement.Children.Add(new Border { Tag = "part.visible", Width = 80, Height = 36 }); placement.Children.Add(new Border { Tag = "part.hidden", Width = 80, Height = 36, Visibility = Visibility.Collapsed }); placement.Measure(new Size(600, 40)); placement.Arrange(new Rect(0, 0, 600, 40));
        check("Hidden plugin Dock buttons reserve no slot or spacing", placement.Slots.Count == 1 && placement.Slots[0].Id == "part.visible" && layoutConfig.TopDockItems[1].Position == .7, null);
        string savedOptions = JsonSerializer.Serialize(model.Config.ModOptions); var windows = model.Tracker.Windows.ToArray();
        try
        {
            var stats = tools.First(m => m.Id == "harbor.system"); var notes = tools.First(m => m.Id == "harbor.notes");
            model.Mods.Set(stats, "visibility", PluginVisibility.Selected, false); model.Mods.Set(stats, "visible-apps", "Discord.exe", false);
            model.Mods.Set(notes, "visibility", PluginVisibility.Selected, false); model.Mods.Set(notes, "visible-apps", "opera.exe", false);
            model.Tracker.LoadPreview(new[] { discord });
            check("Different tools independently follow their own chosen apps", PluginVisibility.Allowed(model, stats) && !PluginVisibility.Allowed(model, notes), null);
            model.Tracker.LoadPreview(new[] { opera });
            check("Changing open apps transfers each tool's visibility correctly", !PluginVisibility.Allowed(model, stats) && PluginVisibility.Allowed(model, notes), null);
            model.Mods.Set(stats, "visibility", PluginVisibility.Apps, false); model.Mods.Set(stats, "always-on", "No", false);
            string before = File.ReadAllText(Settings.FilePath);
            var settings = new PluginQuickSettings(model, stats, model.CurrentMonitor());
            var choose = Descendants<Button>(settings).Single(b => AutomationProperties.GetName(b) == PluginVisibility.Selected); choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Choosing app-specific visibility defaults to conditional expansion", settings.Pending["visibility"] == PluginVisibility.Selected && settings.Pending["always-on"] == "Yes", null);
            var checkboxes = Descendants<CheckBox>(settings).Where(c => AutomationProperties.GetName(c).StartsWith("Show with ")).ToArray();
            foreach (var choice in checkboxes) choice.IsChecked = true;
            check("The searchable picker selects several apps for the same tool", settings.Pending["visible-apps"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 2, null);
            check("The new control panel keeps uncommitted changes out of saved settings", File.ReadAllText(Settings.FilePath) == before, null);
            foreach (string page in new[] { "Visibility", "Position", "Appearance" }) settings.PreparePreview(page);
            settings.Close();
            check("Canceling the control panel preserves saved plugin choices", File.ReadAllText(Settings.FilePath) == before, null);
            model.Mods.Set(stats, "always-on", "Yes", false); var panel = new PluginPopup(model, stats, new Widget(model, stats, inline: true), model.CurrentMonitor()); panel.RequestClose();
            check("Hiding a conditional panel preserves its saved expansion mode", model.Mods.Value(stats, "always-on") == "Yes", null);
        }
        finally { model.Config.ModOptions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(savedOptions)!; model.Config.Save(); model.Tracker.LoadPreview(windows); }
    }
    static IEnumerable<T> Descendants<T>(DependencyObject item) where T : DependencyObject
    {
        if (item is T match) yield return match;
        foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) foreach (var nested in Descendants<T>(child)) yield return nested;
    }
}
