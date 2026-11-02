using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class DragonFrameCache
{
    internal sealed record Frame(double Progress, BitmapSource? Image, Rect Bounds);
    internal sealed record Frames(Frame[] Items, double PreparationMilliseconds, long Bytes)
    {
        internal Frame At(double progress) { double t = progress < .5 ? Math.Cbrt(progress / 4) : 1 - Math.Cbrt((1 - progress) / 4); return Items[Math.Clamp((int)Math.Round(t * (Items.Length - 1)), 0, Items.Length - 1)]; }
    }
    sealed record Spec(Point Origin, Rect Home, double Size, double Radius, double Width, double Height, Color Logo, Color Fire, Color Panel, Color Border);
    static readonly object gate = new();
    static readonly Dictionary<string, Task<Frames>> cache = new();
    static double Rounded(double n) => Math.Round(n * 2) / 2;
    internal static Task<Frames> Get(DragonHomeFire fire)
    {
        var spec = new Spec(new Point(Rounded(fire.Origin.X - fire.RenderOffset.X), Rounded(fire.Origin.Y - fire.RenderOffset.Y)), new Rect(Rounded(fire.HomeBounds.X - fire.RenderOffset.X), Rounded(fire.HomeBounds.Y - fire.RenderOffset.Y), fire.HomeBounds.Width, fire.HomeBounds.Height), fire.LogoSize, fire.CornerRadius, fire.Width, fire.Height, Theme.CurrentColors.Logo, Theme.CurrentColors.Fire, Theme.Panel.Color, Theme.Line.Color);
        string key = string.Join("/", spec.Origin.ToString(CultureInfo.InvariantCulture), spec.Home.ToString(CultureInfo.InvariantCulture), spec.Size, spec.Radius, spec.Width, spec.Height, spec.Logo, spec.Fire, spec.Panel, spec.Border);
        lock (gate)
        {
            if (cache.TryGetValue(key, out var task)) return task;
            if (cache.Count >= 2) cache.Clear();
            var completion = new TaskCompletionSource<Frames>(TaskCreationOptions.RunContinuationsAsynchronously); cache[key] = completion.Task;
            var worker = new Thread(() => { try { completion.SetResult(Build(spec)); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true, Name = "Harbor dragon frames" }; worker.SetApartmentState(ApartmentState.STA); worker.Start(); return completion.Task;
        }
    }
    internal static void Warm(Shell shell, IntPtr monitor)
    {
        if (!Theme.Dragon || shell.Config.DockMotionMilliseconds == 0 || Settings.TestDataDir != null) return;
        var work = PluginSnapping.WorkArea(shell, monitor); var display = Native.Monitor(monitor).Monitor; double dpi = Native.Scale(monitor), width = work.Width / dpi, height = work.Height / dpi;
        var home = new Rect((width - Math.Min(shell.Config.HomeWidth, width - 36)) / 2, (height - Math.Min(shell.Config.HomeHeight, height - 36)) / 2, Math.Min(shell.Config.HomeWidth, width - 36), Math.Min(shell.Config.HomeHeight, height - 36));
        var origin = new Point((display.Left + display.Width / 2d - work.Left) / dpi, (display.Top + display.Height / 2d - work.Top) / dpi); double size = shell.Config.DesktopLogoSize;
        var bounds = home; bounds.Union(new Rect(origin.X - size, origin.Y - size, size * 2, size * 2)); bounds.Inflate(28, 28); bounds.Intersect(new Rect(0, 0, width, height));
        var model = new DragonHomeFire { Origin = origin, HomeBounds = home, LogoSize = size, CornerRadius = shell.Config.HomeCornerRadius, Width = bounds.Width, Height = bounds.Height, RenderOffset = bounds.TopLeft };
        _ = Get(model).ContinueWith(t => { if (t.IsFaulted) Log.Write("Dragon frame warm-up: " + t.Exception?.GetBaseException().Message); }, TaskScheduler.Default);
    }
    static Frames Build(Spec spec)
    {
        var watch = Stopwatch.StartNew(); const int count = 160; var frames = new Frame[count]; long bytes = 0;
        var scene = new DragonHomeFire { Origin = spec.Origin, HomeBounds = spec.Home, LogoSize = spec.Size, CornerRadius = spec.Radius, Width = spec.Width, Height = spec.Height, LogoTint = spec.Logo, FireTint = spec.Fire, PanelTint = spec.Panel, BorderTint = spec.Border };
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)(count - 1), progress = t < .5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; scene.Progress = progress;
            var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) scene.DrawFrame(dc);
            Rect bounds = drawing.ContentBounds; BitmapSource? image = null;
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0) { bounds.Inflate(2, 2); bounds.Intersect(new Rect(0, 0, spec.Width, spec.Height)); if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0) { var translated = new DrawingVisual(); using (var dc = translated.RenderOpen()) { dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y)); dc.DrawDrawing(drawing.Drawing); dc.Pop(); } image = Bitmap(translated, bounds.Width, bounds.Height, .50); bytes += image.PixelWidth * (long)image.PixelHeight * 4; } }
            frames[i] = new Frame(progress, image, bounds);
        }
        return new Frames(frames, watch.Elapsed.TotalMilliseconds, bytes);
    }
    static BitmapSource Bitmap(Visual visual, double width, double height, double scale)
    {
        int w = Math.Max(1, (int)Math.Ceiling(width * scale)), h = Math.Max(1, (int)Math.Ceiling(height * scale)); var rendered = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32); rendered.Render(visual);
        // Materialize pixels on the preparation thread. Playback must not wait
        // for a different rendering channel to read back a RenderTargetBitmap.
        var pixels = new byte[w * h * 4]; rendered.CopyPixels(pixels, w * 4, 0); var bitmap = BitmapSource.Create(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32, null, pixels, w * 4); bitmap.Freeze(); return bitmap;
    }
}
