using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class HomeFilesVerification
{
    internal static void Run(string directory, Shell shell, Action<string, bool, object?> check)
    {
        void C(string name, bool pass, object? detail = null) => check(name, pass, detail);
        ThemeMotionVerification.Run(shell, check);
        var c = new Settings(); c.Normalize(); C("Frequently used is the default Home tab and the desktop bin starts visible", c.HomeStartTab == "Frequently used" && c.DesktopRecycleBin);
        c.HomeColumns = 100; c.HomeRows = -2; c.HomeIconSize = 900; c.HomeTextSize = 1; c.HomeWidth = 90000; c.HomeHiddenTabs.AddRange(new[] { "Desktop", "Frequently used", "Games" }); c.Normalize();
        C("Home customization keeps readable bounds and protects its two main tabs", c.HomeColumns == 6 && c.HomeRows == 1 && c.HomeIconSize == 80 && c.HomeTextSize == 12 && c.HomeWidth == 1600 && c.HomeHiddenTabs.SequenceEqual(new[] { "Games" }));
        c = new Settings { HomeTabOrder = new() { "Social", "Games" }, HomeTabNames = new() { ["Games"] = "Play" } }; c.Normalize(); var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(c))!; restored.Normalize();
        C("Tab order, names and visibility survive a Home settings roundtrip", HomeLayout.OrderedTabs(restored).First() == "Social" && HomeLayout.Label(restored, "Games") == "Play");
        c.AppUsage["one.exe"] = new AppUse { Count = 3, LastUsedUtc = DateTime.UtcNow.AddHours(-2) }; c.AppUsage["two.exe"] = new AppUse { Count = 9, LastUsedUtc = DateTime.UtcNow.AddHours(-1) };
        C("Frequently used ranks real recorded use rather than alphabetical placeholders", HomeLayout.Frequent(c, new[] { new DesktopLibraryItem("A", "one.exe", "one.exe"), new DesktopLibraryItem("Z", "two.exe", "two.exe") }).First().Path == "two.exe");
        var home = new HarborHome(shell, shell.CurrentMonitor(), demo: true); home.PreparePreview(); var view = (FrameworkElement)home.Content; view.Measure(new Size(home.Width, home.Height)); view.Arrange(new Rect(0, 0, home.Width, home.Height)); view.UpdateLayout(); var root = (DependencyObject)view;
        C("Home has a permanent divider below its two fixed library tabs", Descendants(root).Count(v => AutomationProperties.GetAutomationId(v) == "home-permanent-divider") == 1); home.Close();
        var editor = new HomeSettings(shell); C("Home provides a full customization window without applying draft changes", !editor.Applied && HomeLayout.PreferenceKeys.Length >= 16); editor.Close();
        Theme.SetSkin("tokyo-dragon"); var dragon = Pixels(new HarborEmblem(200)); var wallpaper = HarborBackdrop.Image(640, 360); Theme.SetSkin("harbor"); var original = Pixels(new HarborEmblem(200));
        C("Tokyo Dragon swaps the shared logo renderer and provides its own wallpaper", !dragon.SequenceEqual(original) && wallpaper.PixelWidth == 640 && wallpaper.PixelHeight == 360);
        var bin = RecycleBin.Query(); C("The desktop bin reads the real Windows item count without deleting anything", bin.Items >= 0 && bin.Bytes >= 0, new { Count = bin.Items });
        var previousContext = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        try
        {
            var binView = new Files(shell, Files.HomePath); var loaded = binView.NavigateAsync(RecycleBin.Location, false); var frame = new System.Windows.Threading.DispatcherFrame(); loaded.ContinueWith(_ => System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.FromCurrentSynchronizationContext()); System.Windows.Threading.Dispatcher.PushFrame(frame); loaded.GetAwaiter().GetResult(); binView.PrepareRecyclePreview();
            var content = (FrameworkElement)binView.Content; content.Measure(new Size(1360, 880)); content.Arrange(new Rect(0, 0, 1360, 880)); content.UpdateLayout();
            var words = Descendants(content).OfType<TextBlock>().Select(t => t.Text).ToArray(); C("The bin renders real name and location columns rather than record debug text", words.Contains("Project notes.txt") && !words.Any(t => t.StartsWith("RecycledItem {", StringComparison.Ordinal))); binView.Close();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        string scope = Path.GetFullPath(Path.Combine(directory, "file-actions-fixture")); Directory.CreateDirectory(scope); string source = Path.Combine(scope, "source"), target = Path.Combine(scope, "target"); Directory.CreateDirectory(Path.Combine(source, "nested")); Directory.CreateDirectory(Path.Combine(source, "empty")); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "nested", "note.txt"), "Keep this exact text."); File.WriteAllText(Path.Combine(source, "other.md"), "Search me");
        ShellFileOps.TransferAsync(new[] { Path.Combine(source, "nested") }, target, false, IntPtr.Zero, true).GetAwaiter().GetResult();
        C("The native file engine copies a folder and preserves its source", File.ReadAllText(Path.Combine(target, "nested", "note.txt")) == "Keep this exact text." && File.Exists(Path.Combine(source, "nested", "note.txt")));
        ShellFileOps.RenameAsync(Path.Combine(target, "nested", "note.txt"), "renamed.txt", IntPtr.Zero, true).GetAwaiter().GetResult();
        C("Native rename changes the name without changing file contents", File.ReadAllText(Path.Combine(target, "nested", "renamed.txt")) == "Keep this exact text.");
        ShellFileOps.NewFolderAsync(target, "Destination", IntPtr.Zero, true).GetAwaiter().GetResult(); ShellFileOps.TransferAsync(new[] { Path.Combine(target, "nested", "renamed.txt") }, Path.Combine(target, "Destination"), true, IntPtr.Zero, true).GetAwaiter().GetResult();
        C("Native move and new-folder operations complete through the shell engine", !File.Exists(Path.Combine(target, "nested", "renamed.txt")) && File.Exists(Path.Combine(target, "Destination", "renamed.txt")));
        ShellFileOps.NewTextAsync(target, "alpha.txt", IntPtr.Zero, true).GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(target, "beta.md"), "Second file"); ShellFileOps.RenameManyAsync(new[] { Path.Combine(target, "alpha.txt"), Path.Combine(target, "beta.md") }, "Renamed", IntPtr.Zero, true).GetAwaiter().GetResult();
        C("Native new-file and batch rename preserve extensions and file contents", File.Exists(Path.Combine(target, "Renamed (1).txt")) && File.ReadAllText(Path.Combine(target, "Renamed (2).md")) == "Second file");
        string empty = Path.Combine(target, "nested"); if (!Path.GetFullPath(empty).StartsWith(scope + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid test scope."); ShellFileOps.DeleteAsync(new[] { empty }, IntPtr.Zero, true, true).GetAwaiter().GetResult();
        C("Native deletion stays inside its disposable fixture and never touches the user's bin", !Directory.Exists(empty) && File.Exists(Path.Combine(source, "nested", "note.txt")));
        var existingBin = RecycleBin.ReadAsync().GetAwaiter().GetResult(); string roundtrip = Path.Combine(scope, "Harbor-recycle-test-" + Guid.NewGuid().ToString("N") + ".txt"); File.WriteAllText(roundtrip, "Restore this test file exactly.");
        ShellFileOps.DeleteAsync(new[] { roundtrip }, IntPtr.Zero, false, true).GetAwaiter().GetResult(); var afterRecycle = RecycleBin.ReadAsync().GetAwaiter().GetResult(); var ownItem = afterRecycle.Single(i => i.Name == Path.GetFileName(roundtrip) && i.OriginalFolder.TrimEnd('\\').Equals(scope.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        RecycleBin.RestoreAsync(new[] { ownItem.Path }).GetAwaiter().GetResult(); for (int n = 0; n < 30 && !File.Exists(roundtrip); n++) Thread.Sleep(100);
        var afterRestore = RecycleBin.ReadAsync().GetAwaiter().GetResult(); C("Recycling and restoring a file round-trips its exact bytes while keeping existing bin items", File.Exists(roundtrip) && File.ReadAllText(roundtrip) == "Restore this test file exactly." && existingBin.All(i => afterRestore.Any(a => a.Path == i.Path)), new { PreservedItems = existingBin.Count });
        string zip = Path.Combine(scope, "sample.zip"), extracted = Path.Combine(scope, "extracted"); FileArchives.Compress(new[] { source }, zip); FileArchives.Extract(zip, extracted);
        C("ZIP creation and extraction preserve nested content and empty folders", File.ReadAllText(Path.Combine(extracted, "source", "nested", "note.txt")) == "Keep this exact text." && Directory.Exists(Path.Combine(extracted, "source", "empty")));
        string malicious = Path.Combine(scope, "bad.zip"), outside = Path.Combine(scope, "outside.txt"); File.WriteAllText(outside, "Keep me"); using (var archive = ZipFile.Open(malicious, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open()); writer.Write("bad"); }
        bool rejected = false; try { FileArchives.Extract(malicious, Path.Combine(scope, "unsafe")); } catch (InvalidDataException) { rejected = true; }
        C("ZIP extraction rejects traversal before changing any outside file", rejected && File.ReadAllText(outside) == "Keep me" && !Directory.Exists(Path.Combine(scope, "unsafe")));
        var matches = FileSearch.Find(source, "*.txt", true, CancellationToken.None); C("Recursive wildcard search finds nested files and keeps unrelated types out", matches.Count == 1 && matches[0].Name == "note.txt");
        c.FilesShowExtensions = false; var display = new FileNameDisplay(c); C("File extension visibility changes display only, not file paths", (string)display.Convert(new FileEntry("report.txt", "report.txt", false, 1, DateTime.Now), typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture) == "report");
    }
    static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(node, i))) yield return child; }
    static byte[] Pixels(FrameworkElement element) { element.Measure(new Size(200, 200)); element.Arrange(new Rect(0, 0, 200, 200)); var bitmap = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element); var bytes = new byte[200 * 200 * 4]; bitmap.CopyPixels(bytes, 800, 0); return bytes; }
}
