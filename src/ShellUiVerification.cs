using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Harbor;

internal static class ShellUiVerification
{
    internal static void Run(string directory, Action<string, bool, object?> check)
    {
        var c = new Settings { ModsInitialized = true, ShowClock = false, ShowMedia = false }; c.Normalize();
        check("Dock layout migration preserves existing clock and media choices", c.TopDockItems.Count(i => BarLayout.Labels.ContainsKey(i.Id)) == 15 && c.TopDockItems.Where(i => i.Id is "clock" or "date" or "time" or "previous" or "play" or "next").All(i => !i.Enabled), null);
        BarLayout.Move(c, "files", "apps", "left");
        check("Dock buttons move across groups without duplicating", c.TopDockItems[0].Id == "files" && c.TopDockItems[0].Group == "left" && c.TopDockItems.Count(i => i.Id == "files") == 1, null);
        var divider = BarLayout.AddDivider(c, "left", "apps"); var space = BarLayout.AddDivider(c, "center", space: true);
        check("Dock dividers can be placed before a chosen button", c.TopDockItems.IndexOf(divider) + 1 == c.TopDockItems.FindIndex(i => i.Id == "apps") && space.Space, null);
        c.TaskbarDividers.Add(new TaskbarDivider { Before = "notepad.exe", Space = true });
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(c))!; restored.Normalize();
        check("Dock order, visibility, groups and taskbar dividers survive serialization", restored.TopDockItems[0].Id == "files" && !restored.TopDockItems.First(i => i.Id == "clock").Enabled && restored.TaskbarDividers.Single().Before == "notepad.exe", null);
        c.TopDockEnabled = false; check("A hidden Dock releases its reserved screen space", BarLayout.ReservedTop(c, true) == 0, null);
        c.TopDockEnabled = true; c.TopDockReserveSpace = false; check("Overlay mode releases the Dock work area", BarLayout.ReservedTop(c, true) == 0, null);
        c.TopDockReserveSpace = true; c.TopDockPrimaryOnly = true; check("Main-display-only Dock reserves space only on that display", BarLayout.ReservedTop(c, false) == 0 && BarLayout.ReservedTop(c, true) == c.TopBarHeight, null); c.TopDockPrimaryOnly = false;
        c.PreviewFramesPerSecond = 1000; c.Normalize(); check("Preview frame rates stay within the supported range", c.PreviewFramesPerSecond == 30 && ModDemoScene.Frames == 96 && Math.Abs(ModDemoScene.FrameMilliseconds - 1000d / 30) < .01, null);
        var model = new Shell(c); var bar = new TopBar(model, model.CurrentMonitor()); bar.PreparePreview();
        bar.MoveButton("mods", "apps", "left");
        check("The Dock drop action updates and saves button order", c.TopDockItems.FindIndex(i => i.Id == "mods") + 1 == c.TopDockItems.FindIndex(i => i.Id == "apps") && Settings.Load().TopDockItems.First(i => i.Id == "mods").Group == "left", null);
        c.TopDockFontSize = 15; bar.RefreshLayout(); c.TopDockFontSize = 12; bar.RefreshLayout();
        check("Dock layout rebuilds safely while reusing clock and title content", Elements(bar).OfType<FrameworkElement>().Any(e => AutomationProperties.GetAutomationId(e) == "top-dock:title"), null);
        var clock = c.TopDockItems.First(i => i.Id == "clock"); BarLayout.Enable(c, clock, true); bar.RefreshLayout(); bool visible = Elements(bar).OfType<FrameworkElement>().Any(e => AutomationProperties.GetAutomationId(e) == "top-dock:clock"); BarLayout.Enable(c, clock, false); bar.RefreshLayout();
        check("A Dock button can be enabled and hidden independently", visible && !Elements(bar).OfType<FrameworkElement>().Any(e => AutomationProperties.GetAutomationId(e) == "top-dock:clock"), null); bar.Close();
        var profile = Profiles.Capture(c, model.Mods, "Layout test"); var imported = Profiles.Read(JsonSerializer.Serialize(profile, Mods.Json));
        check("Desktop profiles include Dock layout and taskbar dividers", imported.Appearance.ContainsKey("TopDockItems") && imported.Appearance.ContainsKey("TaskbarDividers"), null);
        string before = File.ReadAllText(Settings.FilePath); var controls = new TopDockControls(model, model.CurrentMonitor()); controls.PreparePreview("Style"); controls.PreparePreview("Behavior"); controls.Close();
        check("Opening Dock controls does not save or change settings", File.ReadAllText(Settings.FilePath) == before, null);
        DockPlacementVerification.Run(check);
        AppFolderVerification.Run(check);
        var a = new FileTab(@"C:\Alpha"); var b = new FileTab(@"C:\Beta"); a.Visit(@"C:\Alpha\Child", true); b.Search = "notes";
        check("File tabs keep their own history and search", a.Previous() == @"C:\Alpha" && b.Back.Count == 0 && b.Search == "notes", null);
        var entries = new[] { new FileEntry("b", "beta.txt", false, 90, DateTime.Today), new FileEntry("folder", "Zulu", true, 0, DateTime.Today), new FileEntry("a", "alpha.txt", false, 10, DateTime.Today) };
        check("File sorting keeps folders first and sorts the selected field", FileBrowserState.Query(entries, "", "Size", true).Select(e => e.Name).SequenceEqual(new[] { "Zulu", "beta.txt", "alpha.txt" }), null);
        check("File search filters names without changing the source list", FileBrowserState.Query(entries, "ALPHA", "Name", false).Single().Name == "alpha.txt" && entries.Length == 3, null);
        string fixture = Path.Combine(directory, "file-browser"), child = Path.Combine(fixture, "Projects"); Directory.CreateDirectory(child); File.WriteAllText(Path.Combine(fixture, "Readme.txt"), "Preview text from a test file."); File.WriteAllText(Path.Combine(fixture, "sample.json"), "{}");
        string source = Path.Combine(fixture, "Readme.txt"), copy = Path.Combine(fixture, "Copy.txt"), moved = Path.Combine(child, "Moved.txt"); FileTools.Transfer(source, copy, false); FileTools.Transfer(copy, moved, true);
        check("File copy and move preserve content and leave the original copy source intact", File.Exists(source) && !File.Exists(copy) && File.ReadAllText(moved) == File.ReadAllText(source), null);
        bool rejected = false; try { FileTools.Transfer(child, Path.Combine(child, "Nested"), true); } catch (IOException) { rejected = true; }
        check("Moving a folder into itself is rejected before changing it", rejected && Directory.Exists(child) && !Directory.Exists(Path.Combine(child, "Nested")), null);
        rejected = false; try { FileTools.Transfer(source, moved, true); } catch (IOException) { rejected = true; }
        check("File operations cannot silently overwrite an existing destination", rejected && File.Exists(source) && File.Exists(moved), null);
        var previous = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try {
            var files = new Files(model, fixture); Pump(files.NavigateAsync(fixture, false));
            var list = Elements(files).OfType<ListView>().Single();
            check("The new file browser loads actual directory entries", files.CurrentFolder == Path.GetFullPath(fixture) && list.Items.OfType<FileEntry>().Any(e => e.Name == "Readme.txt"), null);
            files.PreparePreview("Readme.txt"); Pump(Task.Delay(80));
            check("The preview pane reads selected text without launching it", Elements(files).OfType<TextBox>().Any(t => t.IsReadOnly && t.Text.Contains("Preview text from a test file.")), null);
            files.NewTab(child); Pump(files.NavigateAsync(child, false));
            check("Opening a file tab preserves the previous tab", files.TabCount == 2 && files.CurrentFolder == child, null);
            files.PrepareGridPreview(); var content = (FrameworkElement)files.Content; content.Measure(new Size(1240, 830)); content.Arrange(new Rect(0, 0, 1240, 830)); content.UpdateLayout();
            check("Grid view uses icon cards instead of a details presenter", list.View == null && list.ItemTemplate != null, null); files.Close();
        } finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    static void Pump(Task task) { if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; } var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher; task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false))); Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult(); }
    static IEnumerable<DependencyObject> Elements(DependencyObject node) { yield return node; foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var e in Elements(child)) yield return e; }
}
