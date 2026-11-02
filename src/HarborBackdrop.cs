using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

// An original, static wallpaper. Rasterize once rather than repainting gradients
// across two monitors each time the small emblem changes.
internal static class HarborBackdrop
{
    static readonly object gate = new();
    static readonly Dictionary<(int, int, string), BitmapSource> cache = new();
    internal static ImageBrush Brush(int width, int height)
    {
        var brush = new ImageBrush(Image(width, height)) { Stretch = Stretch.Fill }; brush.Freeze(); return brush;
    }
    internal static ImageBrush Brush(int width, int height, Rect crop)
    {
        var image = Image(width, height);
        var brush = new ImageBrush(image) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(crop.X * image.PixelWidth / width, crop.Y * image.PixelHeight / height, crop.Width * image.PixelWidth / width, crop.Height * image.PixelHeight / height) };
        brush.Freeze(); return brush;
    }
    internal static BitmapSource Image(int width, int height)
    {
        width = Math.Clamp(width, 320, 3840); height = Math.Clamp(height, 200, 2160);
        lock (gate)
        {
            string key = Theme.Skin + "/" + Theme.PaletteKey;
            if (cache.TryGetValue((width, height, key), out var saved)) return saved;
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
            {
                if (Theme.Dragon)
                {
                    var art = DragonImage();
                    dc.DrawRectangle(new SolidColorBrush(Theme.CurrentColors.Wallpaper), null, new Rect(0, 0, width, height));
                    double factor = Math.Min(width / (double)art.PixelWidth, height / (double)art.PixelHeight);
                    dc.DrawImage(art, new Rect(0, (height - art.PixelHeight * factor) / 2, art.PixelWidth * factor, art.PixelHeight * factor));
                }
                else
                {
                dc.PushTransform(new ScaleTransform(width / 1600d, height / 900d));
                dc.DrawRectangle(Fill("#050A13", "#0B1B30", 35), null, new Rect(0, 0, 1600, 900));
                var glow = new RadialGradientBrush { Center = new Point(.5, .48), GradientOrigin = new Point(.5, .48), RadiusX = .54, RadiusY = .72 };
                var light = Theme.CurrentColors.Accent; light.A = 65; glow.GradientStops.Add(new GradientStop(light, 0)); light.A = 0; glow.GradientStops.Add(new GradientStop(light, 1)); glow.Freeze(); dc.DrawRectangle(glow, null, new Rect(0, 0, 1600, 900));
                Plane(new[] { new Point(0, 0), new Point(690, 0), new Point(160, 420), new Point(0, 380) }, "#10243D", "#07111E");
                Plane(new[] { new Point(0, 0), new Point(160, 420), new Point(0, 760) }, "#071526", "#0A1E35");
                Plane(new[] { new Point(1600, 220), new Point(1170, 640), new Point(1470, 900), new Point(1600, 900) }, "#0B233D", "#07111F");
                Plane(new[] { new Point(1170, 640), new Point(865, 900), new Point(1470, 900) }, "#0D2C49", "#081729");
                var seamColor = Theme.CurrentColors.Accent; seamColor.A = 35; var seam = new Pen(new SolidColorBrush(seamColor), 1);
                dc.DrawLine(seam, new Point(0, 548), new Point(690, 0)); dc.DrawLine(seam, new Point(865, 900), new Point(1600, 220));
                seamColor.A = 8; var fine = new Pen(new SolidColorBrush(seamColor), 1); for (int i = 0; i < 5; i++) { double x = 1240 + i * 36; dc.DrawLine(fine, new Point(x, 900), new Point(1600, 545 - i * 36)); }
                dc.Pop();
                }
                void Plane(Point[] points, string a, string b) { var shape = new StreamGeometry(); using (var g = shape.Open()) { g.BeginFigure(points[0], true, true); g.PolyLineTo(points.AsSpan(1).ToArray(), true, false); } shape.Freeze(); dc.DrawGeometry(Fill(a, b, 70), null, shape); }
            }
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
            if (cache.Count >= 6) cache.Clear(); cache[(width, height, key)] = image; return image;
        }
    }
    static Color Tone(string original) { var c = (Color)ColorConverter.ConvertFromString(original); return Theme.Mix(Theme.Mix(Colors.Black, Theme.CurrentColors.Wallpaper, .5), Theme.CurrentColors.Accent, Math.Max(c.R, Math.Max(c.G, c.B)) / 255d * .42); }
    static Brush Fill(string a, string b, double angle) { var brush = new LinearGradientBrush(Tone(a), Tone(b), angle); brush.Freeze(); return brush; }
    static BitmapSource DragonImage()
    {
        var source = new BitmapImage(new Uri("pack://application:,,,/Harbor;component/Assets/tokyo-dragon-outline.png"));
        var format = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); int stride = format.PixelWidth * 4; var pixels = new byte[stride * format.PixelHeight]; format.CopyPixels(pixels, stride, 0);
        var background = Theme.CurrentColors.Wallpaper; var ink = Theme.CurrentColors.Logo;
        for (int i = 0; i < pixels.Length; i += 4) { double t = Math.Clamp((pixels[i + 2] - (pixels[i] + pixels[i + 1]) / 2d) / 230d, 0, 1); var color = Theme.Mix(background, ink, t); pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = 255; }
        var image = BitmapSource.Create(format.PixelWidth, format.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride); image.Freeze(); return image;
    }
}
