using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class ModDemoRecorder
{
    internal static void Optimize(string sourceDirectory, string destinationDirectory)
    {
        sourceDirectory = Path.GetFullPath(sourceDirectory); destinationDirectory = Path.GetFullPath(destinationDirectory); Directory.CreateDirectory(destinationDirectory);
        foreach (string path in Directory.EnumerateFiles(sourceDirectory, "*.png"))
        {
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(path));
            if (!path.EndsWith("-film.png", StringComparison.Ordinal)) { File.Copy(path, destination, true); continue; }
            var input = new BitmapImage(); input.BeginInit(); input.CacheOption = BitmapCacheOption.OnLoad; input.UriSource = new Uri(path); input.EndInit();
            var palette = new BitmapPalette(input, 256); var pixels = new byte[input.PixelWidth * input.PixelHeight * 4];
            var rgba = new FormatConvertedBitmap(input, PixelFormats.Bgra32, null, 0); rgba.CopyPixels(pixels, input.PixelWidth * 4, 0);
            var indices = new byte[input.PixelWidth * input.PixelHeight]; var cache = new Dictionary<int, byte>();
            for (int i = 0, j = 0; i < pixels.Length; i += 4, j++) {
                int blue = pixels[i], green = pixels[i + 1], red = pixels[i + 2], key = blue | green << 8 | red << 16;
                if (!cache.TryGetValue(key, out byte chosen)) {
                    int best = int.MaxValue;
                    for (int color = 0; color < palette.Colors.Count; color++) { var c = palette.Colors[color]; int dr = red - c.R, dg = green - c.G, db = blue - c.B, error = dr * dr + dg * dg + db * db; if (error < best) { best = error; chosen = (byte)color; } }
                    cache[key] = chosen;
                }
                indices[j] = chosen;
            }
            Save(BitmapSource.Create(input.PixelWidth, input.PixelHeight, 96, 96, PixelFormats.Indexed8, palette, indices, input.PixelWidth), destination);
        }
        foreach (string name in new[] { "demos.json", "builtins.json" }) { string source = Path.Combine(sourceDirectory, name); if (File.Exists(source)) File.Copy(source, Path.Combine(destinationDirectory, name), true); }
    }
    internal static void ExportCatalog(string path)
    {
        path = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); Settings.TestDataDir = Path.Combine(Path.GetTempPath(), "HarborCatalog-" + Guid.NewGuid().ToString("N"));
        var mods = new Mods(Settings.Load()); File.WriteAllText(path, JsonSerializer.Serialize(mods.Catalog.Where(m => m.Id.StartsWith("harbor.")).ToList(), Mods.Json));
    }
    internal static void Record(string directory)
    {
        directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
        Settings.TestDataDir = Path.Combine(directory, "profile");
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Theme.Install();
        var model = new Shell(Settings.Load()); var report = new List<object>();
        try {
            foreach (var mod in model.Mods.Catalog.Where(m => m.Id.StartsWith("harbor.")))
            {
                var demo = new ModDemoScene(model, mod);
                demo.Draw(.72); Save(Snapshot(demo.View), Path.Combine(directory, mod.Id + ".png"));
                int width = ModDemoScene.FilmWidth, height = ModDemoScene.FilmHeight, columns = ModDemoScene.Columns;
                var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen())
                    for (int frame = 0; frame < ModDemoScene.Frames; frame++) {
                        demo.Draw(frame / (double)(ModDemoScene.Frames - 1)); var image = Snapshot(demo.View);
                        dc.DrawImage(image, new Rect(frame % columns * width, frame / columns * height, width, height));
                    }
                var strip = new RenderTargetBitmap(columns * width, (ModDemoScene.Frames / columns) * height, 96, 96, PixelFormats.Pbgra32); strip.Render(drawing);
                Save(strip, Path.Combine(directory, mod.Id + "-film.png"));
                report.Add(new { mod.Id, mod.Component, Renderer = demo.Renderer, Caption = demo.Instruction, FrameCount = ModDemoScene.Frames, Poster = mod.Id + ".png", Film = mod.Id + "-film.png" });
            }
            File.WriteAllText(Path.Combine(directory, "demos.json"), JsonSerializer.Serialize(new { Width = ModDemoScene.Width, Height = ModDemoScene.Height, FilmWidth = ModDemoScene.FilmWidth, FilmHeight = ModDemoScene.FilmHeight, Columns = ModDemoScene.Columns, Frames = ModDemoScene.Frames, FrameMilliseconds = ModDemoScene.FrameMilliseconds, FramesPerSecond = 30, SampleData = true, DesktopChanged = false, Items = report }, Mods.Json));
            var overview = new DrawingVisual(); using (var dc = overview.RenderOpen())
            {
                dc.DrawRectangle(Theme.Bg, null, new Rect(0, 0, 1440, 2025)); int index = 0;
                foreach (var mod in model.Mods.Catalog.Where(m => m.Id.StartsWith("harbor."))) {
                    var image = new BitmapImage(new Uri(Path.Combine(directory, mod.Id + ".png")));
                    double x = index % 4 * 360, y = index / 4 * 225;
                    dc.DrawImage(image, new Rect(x, y, 360, 200));
                    dc.DrawText(new FormattedText(mod.Name, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Theme.Text, 1), new Point(x + 8, y + 203)); index++;
                }
            }
            var gallery = new RenderTargetBitmap(1440, 2025, 96, 96, PixelFormats.Pbgra32); gallery.Render(overview); Save(gallery, Path.Combine(directory, "overview.png"));
        } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
        application.Shutdown();
    }
    internal static BitmapSource Snapshot(FrameworkElement element)
    {
        element.Measure(new Size(ModDemoScene.Width, ModDemoScene.Height)); element.Arrange(new Rect(0, 0, ModDemoScene.Width, ModDemoScene.Height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(ModDemoScene.Width, ModDemoScene.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element); bitmap.Freeze(); return bitmap;
    }
    static void Save(BitmapSource source, string path) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using var file = File.Create(path); encoder.Save(file); }
}
