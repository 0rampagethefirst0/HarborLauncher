using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class HomeMotionVerification
{
    // No Show, input hooks, screen capture, clipboard writes or shell restart.
    internal static void Run(string directory)
    {
        directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory); Settings.TestDataDir = Path.Combine(directory, "profile");
        var rows = new List<object>(); int failures = 0; var foreground = Native.GetForegroundWindow();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        void Check(string name, bool passed, object? detail = null) { rows.Add(new { Name = name, Passed = passed, Detail = detail }); if (!passed) failures++; }
        try
        {
            Theme.Install(); var model = new Shell(Settings.Load()); RunChecks(model, Check); ScreenshotVerification.Run(directory, Check); RenderReview(model, directory);
            int visible = 0; Native.EnumWindows((window, _) => { Native.GetWindowThreadProcessId(window, out uint pid); if (pid == Environment.ProcessId && Native.IsWindowVisible(window)) visible++; return true; }, IntPtr.Zero);
            Check("Background verification creates no visible windows and preserves game focus", visible == 0 && Native.GetForegroundWindow() == foreground, new { VisibleWindows = visible });
        }
        catch (Exception ex) { failures++; rows.Add(new { Name = "Verification finished", Passed = false, Detail = ex.ToString() }); }
        File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(rows, Mods.Json)); app.Shutdown(); Environment.ExitCode = failures > 0 ? 1 : 0;
    }
    internal static void RunChecks(Shell model, Action<string, bool, object?> check)
    {
        var physical = new EmblemState(new Rect(-1775, 225, 306, 306), 137.25); var local = physical.RelativeTo(new Point(-1920, 45), 1.25);
        check("Diamond coordinates preserve the exact screen position, hover size and rotation at another DPI", Near(local.Bounds.Left, 116) && Near(local.Bounds.Top, 144) && Near(local.Bounds.Width, 244.8) && Near(local.Turn, 137.25), null);
        check("The same logo returns to its desktop angle and settles into its header pose", new[] { -16d, 0, 73.5, 179.9, 359.9, 721.25 }.All(a => Near(EmblemState.MorphTurn(a, 0), a) && Near(Math.IEEERemainder(EmblemState.MorphTurn(a, 1) + 16, 360), 0)), null);
        check("The logo follows the shortest turn instead of wobbling or spinning repeatedly", Enumerable.Range(0, 101).All(i => Math.Abs(EmblemState.MorphTurn(137.25, i / 100d) - 137.25) <= 180.0001), null);
        var monitor = model.CurrentMonitor(); var work = PluginSnapping.WorkArea(model, monitor); double dpi = Native.Scale(monitor);
        var anchor = new EmblemState(new Rect(work.Left + 390 * dpi, work.Top + 310 * dpi, 244.8 * dpi, 244.8 * dpi), 137.25);
        var home = new HarborHome(model, monitor, anchor, demo: true);
        try
        {
            home.PreparePreview(0); var first = home.FlyingState; home.PreparePreview(.38); home.PreparePreview(0); var closed = home.FlyingState;
            var expected = anchor.RelativeTo(new Point(work.Left, work.Top), dpi);
            check("The closing diamond lands on the background diamond's bounds and orientation", Near(closed.Bounds.Left, expected.Bounds.Left) && Near(closed.Bounds.Top, expected.Bounds.Top) && Near(closed.Bounds.Width, expected.Bounds.Width) && Near(closed.Turn, expected.Turn), null);
            check("Reversing partway through the morph does not change its return point", Near(first.Bounds.Left, closed.Bounds.Left) && Near(first.Bounds.Top, closed.Bounds.Top) && Near(first.Turn, closed.Turn), null);
            var source = Pixels(new HarborEmblem(240) { Turn = anchor.Turn }, 240, 240); var end = Pixels(new HarborEmblem(240) { Turn = closed.Turn }, 240, 240);
            check("The source and returned 3D diamond render identical pixels", source.SequenceEqual(end), null);
            var content = (System.Windows.Controls.Grid)home.Content;
            var returned = Pixels(content, 1280, 900); int upperCenter = (((int)Math.Ceiling(closed.Bounds.Top + 5)) * 1280 + (int)Math.Round(closed.Bounds.Left + closed.Bounds.Width / 2)) * 4;
            check("Closing leaves no dark circular panel behind the diamond", returned[upperCenter + 3] == 0, null);
            check("The full-screen background stays still while the panel morphs", content.Children.OfType<System.Windows.Controls.Border>().All(b => Near(b.Opacity, 1) && ReferenceEquals(b.Background, Brushes.Transparent)), null);
            home.PreparePreview(.38); var before = Pixels(content, 1280, 900); home.PreparePreview(.72); var after = Pixels(content, 1280, 900);
            check("The real cached panel renders distinct frames without detaching its live controls", home.UsesCachedMorph && home.VisibleTiles == 12 && !before.SequenceEqual(after), null);
            check("One logo remains visible throughout the complete transition", new[] { 0d, .2, .4, .6, .8, 1 }.All(p => { home.PreparePreview(p); return Near(home.LogoOpacity, 1) && Descendants<HarborEmblem>(content).Count(e => e.Visibility == Visibility.Visible) == 1; }), null);
            home.PreparePreview(.4); var text = Descendants<System.Windows.Controls.TextBlock>(content).First(t => t.Text == "Steam"); var transform = text.TransformToAncestor(content); var left = transform.Transform(new Point(0, 0)); var right = transform.Transform(new Point(100, 0));
            check("App labels keep their natural scale while the surface opens around them", Near(right.X - left.X, 100), null);
        }
        finally { home.Close(); }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var prepared = new HarborHome(model, monitor, anchor, items: HarborHome.DemoItems()); double construction = watch.Elapsed.TotalMilliseconds;
        try
        {
            prepared.PrepareOpening();
            check("Apps and their first frame are prepared before any native window is created", prepared.ItemCount == 12 && prepared.Prepared && new System.Windows.Interop.WindowInteropHelper(prepared).Handle == IntPtr.Zero, new { ConstructionMilliseconds = construction, prepared.SnapshotMilliseconds });
            double firstSnapshot = prepared.SnapshotMilliseconds; prepared.PrepareOpening();
            check("Starting the opening reuses the prepared image and keeps companion tools deferred", prepared.SnapshotMilliseconds == firstSnapshot && !prepared.Ready, null);
            var handle = new System.Windows.Interop.WindowInteropHelper(prepared).EnsureHandle();
            check("Creating Home's native window does not expose an unfinished surface", !Native.IsWindowVisible(handle) && !prepared.IsVisible, null);
            int result = Native.DwmGetWindowAttribute(handle, 14, out int cloak, sizeof(int));
            check("The compositor keeps Home concealed while its first frame is being prepared", result == 0 && (cloak & 1) == 1, new { Result = result, Cloak = cloak });
        }
        finally { prepared.Close(); }
        var full = HarborBackdrop.Image(640, 400); var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawRectangle(HarborBackdrop.Brush(640, 400, new Rect(0, 40, 640, 320)), null, new Rect(0, 0, 640, 320));
        var cropped = new RenderTargetBitmap(640, 320, 96, 96, PixelFormats.Pbgra32); cropped.Render(drawing); bool matches = true;
        foreach (var p in new[] { new Int32Rect(55, 35, 1, 1), new Int32Rect(320, 160, 1, 1), new Int32Rect(580, 280, 1, 1) }) { byte[] a = new byte[4], b = new byte[4]; full.CopyPixels(new Int32Rect(p.X, p.Y + 40, 1, 1), a, 4, 0); cropped.CopyPixels(p, b, 4, 0); matches &= a.Zip(b).All(v => Math.Abs(v.First - v.Second) <= 2); }
        check("Home uses the same background pixels while respecting the bars' screen space", matches, null);
        var lit = HarborEmblem.Project(-16, 240, 240);
        check("The original emblem has lit bevel geometry and distinct face shading", lit.Any(f => f.Metallic) && lit.Select(f => f.Shade).Distinct().Count() >= 4, null);
    }
    static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T own) yield return own;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    static void RenderReview(Shell model, string directory)
    {
        var home = new HarborHome(model, model.CurrentMonitor(), demo: true); var content = (System.Windows.Controls.Grid)home.Content; content.Background = home.Background;
        try { foreach (double phase in new[] { 0d, .2, .4, .6, .8, 1 }) { home.PreparePreview(phase); var bytes = Pixels(content, 1280, 900); var bitmap = BitmapSource.Create(1280, 900, 96, 96, PixelFormats.Pbgra32, null, bytes, 1280 * 4); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, "home-" + (phase * 100).ToString("000") + ".png")); encoder.Save(file); } }
        finally { home.Close(); }
        foreach (double turn in new[] { -16d, 45d, 135d }) { var view = new System.Windows.Controls.Grid { Background = HarborBackdrop.Brush(420, 420), Children = { new HarborEmblem(300) { Turn = turn, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } }; var bytes = Pixels(view, 420, 420); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(420, 420, 96, 96, PixelFormats.Pbgra32, null, bytes, 420 * 4))); using var file = File.Create(Path.Combine(directory, "emblem-" + turn.ToString("0") + ".png")); encoder.Save(file); }
    }
    static bool Near(double a, double b) => Math.Abs(a - b) < .00001;
    static byte[] Pixels(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout(); var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(element); byte[] bytes = new byte[width * height * 4]; image.CopyPixels(bytes, width * 4, 0); return bytes;
    }
}
