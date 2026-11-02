using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class HomeVerification
{
    internal static void Run(string directory, Shell model, Action<string, bool, object?> check)
    {
        var face = HarborEmblem.Project(-16, 240, 240); var turned = HarborEmblem.Project(70, 240, 240);
        check("The original folded emblem projects real depth with a small software mesh", face.Length > 8 && face.Length < 100 && face.All(f => f.Points.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y))) && face.SelectMany(f => f.Points).All(p => p.X is >= 0 and <= 240 && p.Y is >= 0 and <= 240), face.Length);
        check("Rotating the 3D emblem changes its silhouette and visible faces", !face.SelectMany(f => f.Points).SequenceEqual(turned.SelectMany(f => f.Points)), null);
        var mark = new HarborEmblem(); mark.Spin(true); mark.Spin(false); check("A covered or hidden desktop can stop the emblem's animation clock", !mark.IsSpinning && !mark.HasAnimatedProperties, null);
        var wallpaper = HarborBackdrop.Image(640, 360);
        check("Harbor's new background is cached, frozen and safe across UI threads", wallpaper.IsFrozen && ReferenceEquals(wallpaper, HarborBackdrop.Image(640, 360)), null);
        check("The new Harbor background is enabled while the original Windows wallpaper remains available", new Settings().HarborWallpaper && Profiles.AppearanceKeys.Contains("HarborWallpaper"), null);
        ScreenshotVerification.Run(directory, check);
        HomeMotionVerification.RunChecks(model, check);
        string root = Path.Combine(directory, "desktop-library"); Directory.CreateDirectory(Path.Combine(root, "Projects")); File.WriteAllText(Path.Combine(root, "Rocket League.url"), "[InternetShortcut]\nURL=steam://rungameid/252950"); File.WriteAllText(Path.Combine(root, "Notes.txt"), "Keep this file unchanged");
        var scanned = DesktopLibraryModel.Scan(new[] { root, root }); var settings = new Settings(); settings.Normalize();
        check("The desktop library reads shortcuts, folders and files without duplicates", scanned.Count == 3 && scanned.Any(i => i.Folder) && scanned.Any(i => i.App) && scanned.Any(i => i.Kind == "TXT file"), null);
        check("Automatic categories distinguish games, folders and files", DesktopLibraryModel.Category(settings, scanned.Single(i => i.App)) == "Games" && DesktopLibraryModel.Category(settings, scanned.Single(i => i.Folder)) == "Folders" && DesktopLibraryModel.Category(settings, scanned.Single(i => i.Name == "Notes")) == "Files", null);
        settings.DesktopCustomCategories.Add("My projects"); var item = scanned.First(i => i.Name == "Notes"); DesktopLibraryModel.Move(settings, item.Path, "My projects");
        check("Organizing a category changes metadata while preserving the original files", DesktopLibraryModel.Query(scanned, settings, "My projects", "").Single().Path == item.Path && File.ReadAllText(item.Path) == "Keep this file unchanged" && File.Exists(Path.Combine(root, "Rocket League.url")), null);
        settings.DesktopFavorites.Add(item.Path.ToUpperInvariant());
        check("Favorites and category search find the same desktop item", DesktopLibraryModel.Query(scanned, settings, "Favorites", "notes").Count == 1 && DesktopLibraryModel.Query(scanned, settings, "All", "My projects").Count == 1, null);
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!; restored.Normalize();
        check("Custom categories and favorites survive a settings roundtrip", restored.DesktopCategories[item.Path] == "My projects" && restored.DesktopFavorites.Count == 1, null);
        check("All installed apps stay distinct from the desktop shortcut source", DesktopLibraryModel.AllApps(new[] { new AppEntry { Name = "Notes", Path = "notes.lnk", Target = "notepad.exe" }, new AppEntry { Name = "Notes duplicate", Path = "other.lnk", Target = "notepad.exe" } }).Count == 1, null);
        settings.DesktopLogoSize = 999; settings.Normalize(); check("Desktop emblem size stays within a usable range", settings.DesktopLogoSize == 280, null);
        var rate = new Settings { AnimationFramesPerSecond = 60, PreviewFramesPerSecond = 60 }; rate.Normalize();
        check("Existing settings upgrade to 120 FPS motion and 90 FPS previews", rate.AnimationFramesPerSecond == 120 && rate.PreviewFramesPerSecond == 90, null);
        rate.AnimationFramesPerSecond = 20; rate.PreviewFramesPerSecond = 20; rate.Normalize(); check("The requested eighty-FPS minimum is enforced in both settings", rate.AnimationFramesPerSecond == 80 && rate.PreviewFramesPerSecond == 80, null);
        var startup = new Settings { FeatureVersion = 1, EnabledMods = new() { ShellFeatures.Id("desktop-icons") } };
        check("The startup intro follows the enabled Home feature and its own setting", StartupPresentation.ShouldShow(startup) && !StartupPresentation.ShouldShow(new Settings()), null);
        startup.StartupIntroEnabled = false; check("Turning the startup intro off bypasses the cinematic overlay", !StartupPresentation.ShouldShow(startup), null);
        using (var introProbe = new StartupPresentation(settings, true, true)) { bool ended = introProbe.Wait(5000); check("The startup renderer can use both monitors with Harbor's main theme installed", ended && introProbe.Error == null && introProbe.WindowsCreated == System.Windows.Forms.Screen.AllScreens.Length, introProbe.Error); }
        string saved = File.ReadAllText(Settings.FilePath);
        var home = new HarborHome(model, model.CurrentMonitor(), demo: true); home.PreparePreview();
        check("Harbor Home presents real labeled app tiles in its organized library", home.ItemCount == 12 && home.VisibleTiles == 12, home.VisibleTiles);
        home.PreparePreview(1, "Games"); check("Choosing a Home category filters the actual tile grid", home.VisibleTiles == 2, home.VisibleTiles);
        home.PreparePreview(); home.SearchPreview("Spotify"); check("Home search filters apps by name without launching them", home.VisibleTiles == 1, null);
        home.Close(); check("Previewing the emblem library does not overwrite live choices", File.ReadAllText(Settings.FilePath) == saved, null);
        var art = new StartupArtwork { Progress = .2 }; var early = Pixels(art); art.Progress = 1; var ready = Pixels(art);
        check("The cinematic intro renders different lighting and emblem positions over time", !early.SequenceEqual(ready), null);
    }
    static byte[] Pixels(FrameworkElement element)
    {
        element.Measure(new Size(640, 360)); element.Arrange(new Rect(0, 0, 640, 360)); element.UpdateLayout(); var image = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32); image.Render(element); byte[] bytes = new byte[640 * 360 * 4]; image.CopyPixels(bytes, 640 * 4, 0); return bytes;
    }
}
