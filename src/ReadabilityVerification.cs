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
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Harbor;

internal static class ReadabilityVerification
{
    internal static void Run(string directory, Action<string, bool, object?> check)
    {
        var config = new Settings { ModsInitialized = true, PreviewFramesPerSecond = 30 }; config.Normalize();
        check("Existing profiles migrate to 90 FPS previews and readable default file text", config.PreviewFramesPerSecond == 90 && config.FilesTextSize >= 16 && config.FilesTileSize >= 180, null);
        config.PreviewFramesPerSecond = 20; config.Normalize(); check("The requested eighty-FPS minimum is preserved after migration", config.PreviewFramesPerSecond == 80, null); config.PreviewFramesPerSecond = 90;
        var pins = new[] { new Pin { Name = "Calculator", Path = "calc.exe", Target = "calc.exe" }, new Pin { Name = "Notepad", Path = "notepad.exe", Target = "notepad.exe" }, new Pin { Name = "Paint", Path = "mspaint.exe", Target = "mspaint.exe" } };
        var folder = new AppFolder { Name = "Tools", Apps = pins.ToList() }; config.AppFolders.Add(folder); config.Normalize();
        check("Taskbar folders reserve enough height for a large cover and visible name", config.TaskbarFolderSize >= 48 && config.DockHeight >= config.TaskbarFolderSize + 48, null);
        var shell = new Shell(config); shell.SaveShortcuts(); var dock = new Dock(shell, shell.CurrentMonitor()); dock.Refresh(true);
        var root = (FrameworkElement)dock.Content; root.Measure(new Size(dock.Width, dock.Height)); root.Arrange(new Rect(0, 0, dock.Width, dock.Height)); root.UpdateLayout();
        var folderButton = Elements(dock).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "pin:" + TaskbarFolders.Path(folder));
        check("The actual taskbar folder tile has a readable caption and larger hit area", folderButton.ActualWidth >= 100 && folderButton.ActualHeight >= 80 && Elements(folderButton).OfType<TextBlock>().Any(t => t.Text == "Tools" && t.FontSize >= 13), null); dock.Close();
        var defaults = shell.Mods.Catalog.Where(m => m.Id.StartsWith("harbor.")).ToList();
        check("Every preset contains a specific multi-step usage guide", defaults.Count == 37 + ShellFeatures.Definitions.Length && defaults.All(m => ModHelp.Steps(m).Length >= 3 && m.Instructions.Length > 120), null);
        var note = defaults.First(m => m.Component == "notes"); var custom = Mods.Read(JsonSerializer.Serialize(note, Mods.Json)); custom.Id = "fixture.guide"; custom.Instructions = "Install it.\nType into the note.\nMove it by its header.";
        check("Creator instructions survive manifest export and import", Mods.Read(JsonSerializer.Serialize(custom, Mods.Json)).Instructions == custom.Instructions, null);
        bool rejected = false; custom.Instructions = new string('x', 8001); try { Mods.Validate(custom); } catch (InvalidDataException) { rejected = true; }
        check("Unbounded plugin instructions are rejected", rejected, null);
        check("Plugin option hints explain units and input syntax", ModHelp.OptionHint(defaults.First(m => m.Component == "window-topmost"), new ModOption { Key = "apps" }).Contains("Discord.exe") && ModHelp.OptionHint(note, new ModOption { Key = "width" }).Contains("pixels"), null);
        Motion.Configure(config); var animation = Motion.Animation(0, 1);
        check("Shared motion uses the configured frame target and an easing curve", Timeline.GetDesiredFrameRate(animation) == config.AnimationFramesPerSecond && animation.EasingFunction is CubicEase, null);
        config.DockMotionMilliseconds = 0; var panel = new Border(); Motion.Enter(panel); check("Reduced motion leaves offscreen and disabled surfaces unchanged", !panel.HasAnimatedProperties, null); config.DockMotionMilliseconds = 170;
        var film = ModBanner.DecodeFilmAsync("harbor.calculator").GetAwaiter().GetResult();
        check("Preview films decode safely on a background thread", film?.IsFrozen == true && film.PixelWidth == 6480 && film.PixelHeight == 7200, null);
        var banner = new ModBanner(defaults.First(m => m.Component == "calculator"), shell); banner.PreparePreviewFrame(ModDemoScene.Frames - 1);
        check("The final frame of a 90 FPS preview can be displayed", ModDemoScene.Frames == 288 && Math.Abs(ModDemoScene.FrameMilliseconds - 1000d / 90) < .001, null);
        string fixture = Path.Combine(directory, "readable-files"); Directory.CreateDirectory(fixture); File.WriteAllText(Path.Combine(fixture, "Read me.txt"), "A readable file preview.");
        var previous = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try {
            var files = new Files(shell); check("Files starts on Frequently used without treating its virtual location as a disk path", files.IsHome && files.CurrentFolder == Files.FrequentPath && Elements(files).OfType<TextBlock>().Any(t => t.Text == "Frequently used"), null);
            Pump(files.NavigateAsync(fixture));
            var actions = Elements(files).OfType<Button>().Select(AutomationProperties.GetName).ToHashSet();
            check("File actions use readable text labels", new[] { "Copy", "Cut", "Paste", "Rename", "Recycle", "View options" }.All(actions.Contains), null);
            files.PrepareOverviewPreview(); check("The unselected preview shows useful folder details", Elements(files).OfType<TextBlock>().Any(t => t.Text == "FOLDER DETAILS") && !Elements(files).OfType<TextBlock>().Any(t => t.Text == "Select a file"), null);
            files.SetFileZoom(220); files.PrepareGridPreview(); root = (FrameworkElement)files.Content; root.Measure(new Size(1360, 880)); root.Arrange(new Rect(0, 0, 1360, 880)); root.UpdateLayout();
            check("File card zoom enlarges both cards and names", config.FilesTileSize == 220 && config.FilesTextSize >= 18 && Elements(files).OfType<ListView>().Single().ItemTemplate != null, null);
            Pump(files.NavigateAsync(Files.HomePath)); check("Folder history can cross between Home and disk locations", files.IsHome && Elements(files).OfType<Button>().First(b => AutomationProperties.GetName(b) == "Back").IsEnabled, null);
            files.Close(); check("Closing Files immediately after resizing saves the chosen size", Settings.Load().FilesTileSize == 220, null);
        } finally { SynchronizationContext.SetSynchronizationContext(previous); }
        var card = new FileEntry("file.txt", "file.txt", false, 100, DateTime.Today); check("File cards identify file types without corrupted label separators", card.Kind == "Text document" && card.CardInfo.Contains('\u00B7') && !card.CardInfo.Contains('?'), null);
    }
    static void Pump(Task task) { if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; } var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher; task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false))); Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult(); }
    static IEnumerable<DependencyObject> Elements(DependencyObject node) { yield return node; foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var item in Elements(child)) yield return item; }
}
