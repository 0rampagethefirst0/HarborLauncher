using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

internal static class DockPlacementVerification
{
    static bool Fits(IReadOnlyList<DockSlot> slots, double width) => slots.All(s => s.Left >= -.001 && s.Left + s.Width <= width + .001) && slots.Zip(slots.Skip(1)).All(pair => pair.First.Left + pair.First.Width <= pair.Second.Left + .001);
    static Settings Config() { var c = new Settings { ModsInitialized = true, ShowClock = true, ShowMedia = true, ShowStats = true }; c.Normalize(); return c; }
    internal static void Run(Action<string, bool, object?> check)
    {
        var c = Config(); var items = c.TopDockItems.Where(i => i.Enabled && BarLayout.Legacy(c, i.Id)).ToList();
        var widths = items.ToDictionary(i => i.Id, i => i.Id.StartsWith("divider.") ? 15d : i.Id == "clock" ? 180d : i.Id == "title" ? 140d : 42d);
        int count = 0; bool allFit = true;
        foreach (double width in new[] { widths.Values.Sum(), 1000, 1440, 1920, 2560 })
            foreach (double fraction in new[] { 0, .05, .17, .32, .5, .68, .83, .95, 1 })
                foreach (var item in items) { var slots = DockPlacement.Arrange(items, widths, width, item.Id, fraction); allFit &= Fits(slots, Math.Max(width, widths.Values.Sum())); count++; }
        check("Free Dock placement keeps icons on screen without overlap across widths and edge drops", allFit, new { Scenarios = count });
        var empty = DockPlacement.Arrange(items, widths, 1920, "files", .32);
        check("An icon can be placed in empty space between the old alignment groups", Math.Abs(empty.First(s => s.Id == "files").Center - 1920 * .32) < .001, null);
        DockPlacement.Save(c, empty, 1920);
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(c))!; restored.Normalize();
        var reload = DockPlacement.Arrange(restored.TopDockItems, widths, 1920);
        check("Free Dock positions survive save and reload without jumping", empty.All(s => Math.Abs(reload.First(r => r.Id == s.Id).Left - s.Left) < .001), null);
        check("Saved Dock positions follow the available monitor width", Fits(DockPlacement.Arrange(restored.TopDockItems, widths, 1440), 1440) && Fits(DockPlacement.Arrange(restored.TopDockItems, widths, 2560), 2560), null);
        var filesItem = c.TopDockItems.First(i => i.Id == "files"); filesItem.Position = double.NaN; c.Normalize(); bool invalid = filesItem.Position == null; filesItem.Position = 5; c.Normalize();
        check("Invalid saved positions are normalized before layout", invalid && filesItem.Position == 1, null);
        BarLayout.Move(c, "files", null, "right"); check("Choosing an alignment resets a button's free position", filesItem.Position == null && filesItem.Group == "right", null);
        var model = new Shell(Config()); var bar = new TopBar(model, model.CurrentMonitor()); bar.PreparePreview();
        var surface = bar.PlacementSurface; surface.Measure(new Size(1920, 48)); surface.Arrange(new Rect(0, 0, 1920, 48)); surface.UpdateLayout();
        var panel = bar.PlacementPanel; string before = JsonSerializer.Serialize(model.Config.TopDockItems);
        bar.PreviewPlacement("files", panel.UsableWidth * .32); var previewSlot = panel.Slots.First(s => s.Id == "files"); bar.ClearPlacementPreview();
        check("Canceling a Dock placement preview leaves the saved layout untouched", before == JsonSerializer.Serialize(model.Config.TopDockItems) && Math.Abs(previewSlot.Center - panel.UsableWidth * .32) < .001, null);
        DependencyObject? hit = VisualTreeHelper.HitTest(surface, new Point(600, 20))?.VisualHit; bool covered = false;
        while (hit != null) { if (ReferenceEquals(hit, surface)) { covered = true; break; } hit = VisualTreeHelper.GetParent(hit); }
        check("The entire Dock background is a drop target, including empty space", covered && surface.AllowDrop && panel.AllowDrop, new { covered, Width = panel.UsableWidth });
        bool placed = bar.PlaceButton("files", panel.UsableWidth * .32); surface.UpdateLayout();
        var saved = Settings.Load().TopDockItems.First(i => i.Id == "files");
        check("The real Dock drop action saves the position selected in empty space", placed && Math.Abs(saved.Position!.Value - .32) < .001, null);
        var divider = model.Config.TopDockItems.First(i => i.Id.StartsWith("divider."));
        placed = bar.PlaceButton(divider.Id, panel.UsableWidth * .72); surface.UpdateLayout();
        check("Dividers can also be dragged into empty Dock space", placed && Math.Abs(model.Config.TopDockItems.First(i => i.Id == divider.Id).Position!.Value - .72) < .001, null);
        bar.PlaceButton("files", panel.UsableWidth + 400); surface.UpdateLayout(); var right = panel.Slots.First(s => s.Id == "files");
        bar.PlaceButton("files", -400); surface.UpdateLayout(); var left = panel.Slots.First(s => s.Id == "files");
        check("Dropping beyond either Dock edge clamps to the visible edge", Math.Abs(right.Left + right.Width - panel.UsableWidth) < .001 && Math.Abs(left.Left) < .001, null);
        surface.Measure(new Size(1440, 48)); surface.Arrange(new Rect(0, 0, 1440, 48)); surface.UpdateLayout();
        check("The actual Dock panel adapts to a narrower display without clipped icons", Fits(panel.Slots, panel.UsableWidth) && panel.UsableWidth < 1500, null);
        bar.Close();
    }

    // An isolated, normal window exposes the real Dock controls to input testing
    // without hiding Windows UI, installing hooks, or touching the live profile.
    internal static void Interactive(string directory)
    {
        Directory.CreateDirectory(directory); Settings.TestDataDir = Path.Combine(Path.GetFullPath(directory), "profile");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Theme.Install();
        var model = new Shell(Config()); model.Config.Pins = new() {
            new Pin { Name = "Notepad", Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe"), Target = "notepad.exe" },
            new Pin { Name = "Calculator", Path = @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", Target = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" },
            new Pin { Name = "Paint", Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mspaint.exe"), Target = "mspaint.exe" },
            new Pin { Name = "Files", Path = "explorer.exe", Target = "explorer.exe" }
        }; model.Catalog.Start(); var bar = new TopBar(model, model.CurrentMonitor()); bar.PreparePreview();
        var surface = bar.PlacementSurface; bar.Content = null; surface.Height = 48; surface.VerticalAlignment = VerticalAlignment.Top;
        var root = new Grid { Background = Theme.Bg }; root.Children.Add(surface);
        var note = Theme.Label("Dock: drag freely, including between Date and Time. Taskbar: center drops group apps; edge drops rearrange.", 13, Theme.Muted); note.Margin = new Thickness(18, 78, 18, 0); note.VerticalAlignment = VerticalAlignment.Top; root.Children.Add(note);
        var fileButton = Theme.Button("Open test Files", "Open isolated Files to inspect folder tabs", () => model.OpenFiles()); fileButton.HorizontalAlignment = HorizontalAlignment.Left; fileButton.VerticalAlignment = VerticalAlignment.Top; fileButton.Margin = new Thickness(18, 115, 0, 0); root.Children.Add(fileButton);
        var dock = new Dock(model, model.CurrentMonitor()); dock.Refresh(true); var taskbar = (FrameworkElement)dock.Content; dock.Content = null; taskbar.Height = model.Config.DockHeight; taskbar.Width = dock.Width; taskbar.HorizontalAlignment = HorizontalAlignment.Center; taskbar.VerticalAlignment = VerticalAlignment.Bottom; taskbar.Margin = new Thickness(0, 0, 0, 20); root.Children.Add(taskbar);
        model.ShortcutsChanged += () => { dock.Refresh(true); taskbar.Width = dock.Width; };
        var window = new Window { Title = "Harbor — Dock drag test", Width = 1280, Height = 340, MinWidth = 900, Content = root, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        void Snapshot() => File.WriteAllText(Path.Combine(directory, "dock-drag-state.json"), JsonSerializer.Serialize(new { Isolated = true, Width = bar.PlacementPanel.UsableWidth, Preview = bar.PlacementPanel.Moving, Slots = bar.PlacementPanel.Slots.Select(s => new { s.Id, s.Left, s.Width, s.Center }), Items = model.Config.TopDockItems, Folders = model.Config.AppFolders, Pins = model.Config.Pins }, Mods.Json));
        timer.Tick += (_, _) => Snapshot(); window.Loaded += (_, _) => { Snapshot(); timer.Start(); };
        window.Closed += (_, _) => { timer.Stop(); Snapshot(); model.Command("close-panels"); bar.Close(); dock.Close(); app.Shutdown(); };
        app.Run(window);
    }
}
