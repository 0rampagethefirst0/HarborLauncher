using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

internal static class ScrollVerification
{
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Theme.Install(); Motion.Configure(new Settings { DockMotionMilliseconds = 170 });
        var rows = new List<object>(); var windows = new List<Window>(); int failures = 0;
        IntPtr foreground = Native.GetForegroundWindow();
        // The user may keep using another app during these concealed checks.
        // Watch for our own activation instead of treating any external switch as a failure.
        bool tookFocus = false; int externalFocusChanges = 0;
        Native.WinEventProc focusChanged = (_, _, window, _, _, _, _) => {
            Native.GetWindowThreadProcessId(window, out uint process);
            if (process == Environment.ProcessId) tookFocus = true; else externalFocusChanges++;
        };
        IntPtr focusHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, focusChanged, 0, 0, 0);
        void Check(string name, bool passed, object? detail = null) { rows.Add(new { Name = name, Passed = passed, Detail = detail }); if (!passed) failures++; }
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timeout.Tick += (_, _) => { timeout.Stop(); Check("Scroll checks finish within their time bound", false); app.Shutdown(1); }; timeout.Start();
        Window Show(object content)
        {
            var window = new Window { Width = 440, Height = 300, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None, Title = "Harbor scroll verification", Content = content };
            window.SourceInitialized += (_, _) => { Native.ToolWindow(window); if (!Native.CloakWindow(new WindowInteropHelper(window).Handle, true)) throw new InvalidOperationException("Could not conceal the owned test window."); };
            windows.Add(window); window.Show(); window.Measure(new Size(440, 300)); window.Arrange(new Rect(0, 0, 440, 300)); window.UpdateLayout(); return window;
        }
        ScrollViewer Viewer(bool horizontal = false) => new() { Content = new Border { Width = horizontal ? 2400 : 350, Height = horizontal ? 180 : 3600 }, VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
        void Wheel(UIElement source, int delta = -120) => source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent, Source = source });
        async Task Settle() { await Task.Delay(230); foreach (var window in windows) { window.Measure(new Size(440, 300)); window.Arrange(new Rect(0, 0, 440, 300)); window.UpdateLayout(); } }
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                var events = new List<string>(); var viewer = Viewer(); viewer.ScrollChanged += (_, e) => events.Add($"{e.VerticalOffset:0.0}/{e.ExtentHeight:0.0}"); Motion.SmoothScroll(viewer); Show(viewer); await Settle();
                Check("The concealed test has a real scrollable viewport", viewer.ScrollableHeight > 3000 && viewer.ViewportHeight > 100, new { viewer.ScrollableHeight, viewer.ExtentHeight, viewer.ViewportHeight, viewer.ActualHeight, viewer.IsVisible });
                Wheel(viewer); await Settle(); Check("A wheel gesture scrolls down smoothly", Math.Abs(viewer.VerticalOffset - 96) < 2, new { viewer.VerticalOffset, Events = events.ToArray() });
                viewer.ScrollToVerticalOffset(650); await Task.Delay(40); Wheel(viewer); await Settle();
                Check("Wheel scrolling resumes at a scrollbar or keyboard position, never an old target", Math.Abs(viewer.VerticalOffset - 746) < 2, new { viewer.VerticalOffset });
                Wheel(viewer); await Task.Delay(35);
                viewer.RaiseEvent(new KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(viewer)!, Environment.TickCount, Key.PageDown) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                viewer.ScrollToVerticalOffset(1200); await Settle();
                Check("Keyboard scrolling cancels unfinished wheel motion", Math.Abs(viewer.VerticalOffset - 1200) < 2, new { viewer.VerticalOffset });
                using (ScrollPosition.Keep(viewer)) viewer.Content = new Border { Width = 350, Height = 3700 }; await Settle();
                Check("Replacing refreshed content preserves the viewport after layout", Math.Abs(viewer.VerticalOffset - 1200) < 2, new { viewer.VerticalOffset });
                for (int i = 0; i < 6; i++) { Wheel(viewer); await Task.Delay(15); } await Settle();
                Check("Rapid wheel gestures accumulate and retain their final position", Math.Abs(viewer.VerticalOffset - 1776) < 2, new { viewer.VerticalOffset });
                using (ScrollPosition.Keep(viewer)) viewer.Content = new Border { Width = 350, Height = 520 }; await Settle();
                Check("A shrinking list clamps to its new bottom instead of resetting to the top", viewer.ScrollableHeight > 0 && Math.Abs(viewer.VerticalOffset - viewer.ScrollableHeight) < 2, new { viewer.VerticalOffset, viewer.ScrollableHeight });
                using (ScrollPosition.Keep(viewer, false)) viewer.Content = new Border { Width = 350, Height = 3600 }; await Settle();
                Check("Deliberate page navigation starts at the top", viewer.VerticalOffset == 0);
                viewer.ScrollToVerticalOffset(600); await Task.Delay(40); using (ScrollPosition.Keep(viewer, false)) { } Wheel(viewer); await Settle();
                Check("Fresh input cancels a queued viewport restoration", Math.Abs(viewer.VerticalOffset - 696) < 2, new { viewer.VerticalOffset });
                var horizontal = Viewer(true); Motion.SmoothScroll(horizontal, true); Show(horizontal); await Settle(); Wheel(horizontal); await Settle();
                horizontal.ScrollToHorizontalOffset(650); await Task.Delay(40); Wheel(horizontal); await Settle();
                Check("Horizontal scrolling also resumes from its current position", Math.Abs(horizontal.HorizontalOffset - 746) < 2, new { horizontal.HorizontalOffset });
                var inner = Viewer(); inner.Height = 160; var panel = new StackPanel(); panel.Children.Add(inner); panel.Children.Add(new Border { Height = 1300 });
                var outer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Motion.SmoothScroll(outer); Motion.SmoothScroll(inner); Show(outer); await Settle();
                Wheel((UIElement)inner.Content); await Settle();
                Check("Nested tools scroll under the pointer without moving their parent", Math.Abs(inner.VerticalOffset - 96) < 2 && outer.VerticalOffset == 0, new { Inner = inner.VerticalOffset, Outer = outer.VerticalOffset });
                // Actual application refresh paths use a separate disposable profile.
                Settings.TestDataDir = Path.Combine(Path.GetFullPath(directory), "profile");
                var model = new Shell(new Settings { FilesView = "details", FeatureVersion = 1 });
                void Conceal(Window window) { window.ShowActivated = false; window.SourceInitialized += (_, _) => { if (!Native.CloakWindow(new WindowInteropHelper(window).Handle, true)) throw new InvalidOperationException("Could not conceal an owned application check."); }; windows.Add(window); window.Show(); }
                T? Find<T>(DependencyObject root, Func<T, bool>? accept = null) where T : DependencyObject { if (root is T match && (accept == null || accept(match))) return match; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (Find(VisualTreeHelper.GetChild(root, i), accept) is { } found) return found; return null; }
                var catalog = new ModCatalog(model); catalog.PreparePreview("detail:harbor.system"); Conceal(catalog); await Settle(); var catalogScroll = Find<ScrollViewer>((DependencyObject)catalog.Content, s => s.Content is StackPanel)!;
                catalogScroll.ScrollToVerticalOffset(700); await Task.Delay(60); double reading = catalogScroll.VerticalOffset;
                var system = model.Mods.Catalog.First(m => m.Id == "harbor.system"); model.Mods.Configure(system, system.Options.ToDictionary(o => o.Key, o => model.Mods.Value(system, o.Key, o.Default))); await Settle();
                Check("A real Mods detail refresh keeps the current reading position", reading > 100 && Math.Abs(catalogScroll.VerticalOffset - reading) < 2, new { Before = reading, After = catalogScroll.VerticalOffset }); catalog.Close();
                string folder = Path.Combine(Path.GetFullPath(directory), "watched-files"); Directory.CreateDirectory(folder); for (int i = 0; i < 430; i++) File.WriteAllText(Path.Combine(folder, $"Item-{i:000}.txt"), "Scroll fixture");
                var files = new Files(model, folder); Conceal(files); await files.NavigateAsync(folder, false); await Settle(); var fileList = Find<ListView>((DependencyObject)files.Content)!; var fileScroll = Find<ScrollViewer>(fileList)!;
                fileScroll.ScrollToVerticalOffset(1200); await Task.Delay(90); double folderReading = fileScroll.VerticalOffset;
                File.WriteAllText(Path.Combine(folder, "Item-500.txt"), "An external update"); await Task.Delay(850); await Settle();
                Check("A watched folder update keeps the real Files viewport", folderReading > 100 && Math.Abs(fileScroll.VerticalOffset - folderReading) < 2, new { Before = folderReading, After = fileScroll.VerticalOffset });
                files.PrepareGridPreview(); await Settle();
                System.Collections.Generic.IEnumerable<DependencyObject> Logical(DependencyObject root) { yield return root; foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var nested in Logical(child)) yield return nested; }
                var next = Logical(files).OfType<Button>().First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Next grid page"); next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Settle(); string first = ((FileEntry)fileList.Items[0]).Path;
                File.WriteAllText(Path.Combine(folder, "Item-501.txt"), "Another external update"); await Task.Delay(850); await Settle();
                Check("Background file updates keep the selected grid page", ((FileEntry)fileList.Items[0]).Path == first); files.Close();
                IntPtr finalForeground = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(finalForeground, out uint finalProcess);
                Check("Concealed scroll checks never take foreground focus", focusHook != IntPtr.Zero && !tookFocus && finalProcess != Environment.ProcessId,
                    new { ForegroundChanged = finalForeground != foreground, ExternalFocusChanges = externalFocusChanges, TookFocus = tookFocus });
            }
            catch (Exception ex) { Check("Scroll verification completes", false, ex.ToString()); }
            finally { timeout.Stop(); foreach (var window in windows) window.Close(); if (focusHook != IntPtr.Zero) Native.UnhookWinEvent(focusHook); GC.KeepAlive(focusChanged); app.Shutdown(failures == 0 ? 0 : 1); }
        }));
        app.Run(); File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(rows, Mods.Json)); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
