using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Harbor;

internal static class AppFolderVerification
{
    internal static void Run(Action<string, bool, object?> check)
    {
        var legacy = new Settings { ModsInitialized = true, TopDockItems = new() { new BarItem { Id = "clock", Group = "center", Position = .5 } } }; legacy.Normalize();
        check("Existing combined clocks migrate into independent date and time pieces", !legacy.TopDockItems.First(i => i.Id == "clock").Enabled && legacy.TopDockItems.Where(i => i.Id is "date" or "time").All(i => i.Enabled && i.Group == "center" && i.Position.HasValue), null);
        var c = new Settings { ModsInitialized = true }; c.Normalize(); BarLayout.Move(c, "play", "time", "center");
        var shell = new Shell(c); var bar = new TopBar(shell, shell.CurrentMonitor()); bar.PreparePreview(); var surface = bar.PlacementSurface; surface.Measure(new Size(1920, 48)); surface.Arrange(new Rect(0, 0, 1920, 48)); surface.UpdateLayout();
        var centers = bar.PlacementPanel.Slots.ToDictionary(s => s.Id, s => s.Center);
        check("Play can sit between the actual date and time controls", centers["date"] < centers["play"] && centers["play"] < centers["time"], null);
        var copy = BarLayout.Duplicate(c, c.TopDockItems.First(i => i.Id == "time")); copy.Format = "HH:mm:ss"; copy.TimeZone = "UTC"; copy.FontSize = 15;
        var custom = new BarItem { Id = "part." + Guid.NewGuid().ToString("N"), Kind = "text", Text = "Load {cpu}% · {ram}%", Group = "left" }; c.TopDockItems.Add(custom); c.Normalize(); bar.RefreshLayout(); bar.PreparePreview();
        check("Dock pieces can be duplicated with independent format, time zone and size", copy.Kind == "time" && c.TopDockItems.Count(i => BarLayout.Component(i) == "time") == 2 && Descendants(bar).OfType<TextBlock>().Any(t => t.FontSize == 15), null);
        check("Custom Dock text resolves live values", DockPieceText.Render(custom, c, "Test", 12, 34) == "Load 12% · 34%", null);
        var roundtrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(c))!; roundtrip.Normalize(); var savedCopy = roundtrip.TopDockItems.Single(i => i.Id == copy.Id);
        check("Custom pieces retain their format and content across settings reloads", savedCopy.TimeZone == "UTC" && savedCopy.Format == "HH:mm:ss" && roundtrip.TopDockItems.Any(i => i.Text == custom.Text), null);
        var editor = new DockPieceEditor(shell, copy); editor.Close(); bar.Close();
        ShellFeatures.Request(c, "taskbar-folders", true); ShellFeatures.Request(c, "collection-tabs", true);
        c.Pins = Enumerable.Range(1, 5).Select(i => new Pin { Name = "App " + i, Path = @"C:\HarborFixture\App" + i + ".exe", Target = "fixture" + i }).ToList();
        var original = TaskbarFolders.AllApps(c).Select(p => p.Path).Order().ToArray(); var a = c.Pins[0]; var b = c.Pins[1]; var d = c.Pins[3]; var e = c.Pins[4]; var third = c.Pins[2];
        var folder = shell.DropTaskbarApps(new[] { a }, b, "folder")!;
        check("Dropping one app on another creates a folder without losing app shortcuts", c.Pins.Count == 4 && folder.Apps.Count == 2 && TaskbarFolders.AllApps(c).Count() == 5, null);
        TaskbarFolders.Add(c, folder, third); shell.SaveShortcuts();
        check("Dragging another app into a folder moves its taskbar shortcut inside", folder.Apps.Count == 3 && !c.Pins.Contains(third) && TaskbarFolders.AllApps(c).Count() == 5, null);
        TaskbarFolders.Add(c, folder, third, b.Path); check("Folder contents can be reordered by dropping before another app", folder.Apps.IndexOf(third) + 1 == folder.Apps.IndexOf(b), null);
        var other = TaskbarFolders.Create(c, d, e); TaskbarFolders.Rename(c, folder, "Games"); TaskbarFolders.Rename(c, other, "Work"); shell.SaveShortcuts();
        var savedFolders = Settings.Load();
        check("Folder names, contents and taskbar positions persist", savedFolders.AppFolders.Count == 2 && savedFolders.AppFolders.First(f => f.Id == folder.Id).Name == "Games" && savedFolders.Pins.Any(p => p.Name == "Games" && p.Path == TaskbarFolders.Path(folder)), null);
        var previous = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try {
            var files = new Files(shell, TaskbarFolders.Path(folder));
            check("Every taskbar folder receives a dynamic Files tab", files.AppFolderTabCount == 2 && files.CurrentAppFolder == folder, null);
            TaskbarFolders.Rename(c, folder, "Play"); shell.SaveShortcuts();
            check("Renaming a folder updates its Files tab and page immediately", files.Title == "Play — Harbor Files" && Descendants(files).OfType<TextBlock>().Any(t => t.Text == "Play"), null);
            TaskbarFolders.Add(c, other, third, e.Path); shell.SaveShortcuts(); files.OpenAppFolderTab(TaskbarFolders.Path(other));
            check("Moving apps between folders is reflected in the shared Files view", !folder.Apps.Contains(third) && other.Apps.Contains(third) && files.CurrentAppFolder == other && Descendants(files).OfType<Button>().Any(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "file-folder-app:" + third.Path), null);
            TaskbarFolders.ToTaskbar(c, third, TaskbarFolders.Path(folder)); shell.SaveShortcuts();
            check("Dragging an app out of a folder restores an individual taskbar shortcut", c.Pins.Contains(third) && !other.Apps.Contains(third) && TaskbarFolders.AllApps(c).Count() == 5, null);
            var popup = new AppFolderPopup(shell, folder, shell.CurrentMonitor()); var popupContent = (FrameworkElement)popup.Content; popupContent.Measure(new Size(530, 430)); popupContent.Arrange(new Rect(0, 0, 530, 430)); popup.Close();
            check("Folder popups construct with real app icons and management actions", Descendants(popupContent).OfType<Button>().Any(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b).StartsWith("folder-app:")), null);
            TaskbarFolders.Unpack(c, other); shell.SaveShortcuts();
            check("Ungrouping removes the dynamic Files tab and restores its apps", files.AppFolderTabCount == 1 && !c.AppFolders.Contains(other) && c.Pins.Any(p => p.Path == e.Path), null);
            files.Close();
        } finally { SynchronizationContext.SetSynchronizationContext(previous); }
        TaskbarFolders.Unpack(c, folder); shell.SaveShortcuts();
        check("Ungrouping all folders preserves every original app shortcut", c.AppFolders.Count == 0 && TaskbarFolders.AllApps(c).Select(p => p.Path).Order().SequenceEqual(original), null);
        var data = TaskbarFolders.Data(a); check("App drag payloads preserve the app identity", TaskbarFolders.Read(data)?.Target == a.Target && TaskbarFolders.Read(new DataObject(TaskbarFolders.DragFormat, "invalid")) == null, null);
        var empty = new AppFolder { Name = "   " }; c.AppFolders.Add(empty); c.Normalize();
        check("Empty named folders remain available on the taskbar and in Files", empty.Name == "Folder" && c.Pins.Any(p => p.Path == TaskbarFolders.Path(empty)), null);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject node) { yield return node; foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var item in Descendants(child)) yield return item; }
}
