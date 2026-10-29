using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class Diagnostics
{
    internal static void Probe(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(new {
            Taskbars = Native.Taskbars().Select(h => new { Handle = h.ToInt64(), Visible = Native.IsWindowVisible(h) }),
            Icons = Native.DesktopIconWindows().Select(h => new { Handle = h.ToInt64(), Visible = Native.IsWindowVisible(h) }),
            Monitors = System.Windows.Forms.Screen.AllScreens.Select(s => { var m = Native.Monitor(Native.MonitorFromPoint(new Native.Point { X = s.Bounds.Left + 1, Y = s.Bounds.Top + 1 }, 2)); return new { m.Device, Work = new { m.Work.Left, m.Work.Top, m.Work.Right, m.Work.Bottom } }; })
        }, Mods.Json));
    }
    internal static void Visual(string directory)
    {
        Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Theme.Install();
        var visualSettings = Settings.Load(); Settings.TestDataDir = Path.Combine(Path.GetFullPath(directory), "profile"); visualSettings.Save();
        var model = new Shell(visualSettings);
        model.Catalog.Changed += async () =>
        {
            try
            {
                await Task.WhenAll(model.Config.Pins.Select(p => Icons.Get(p.Path)).Concat(model.Config.AppFolders.SelectMany(f => f.Apps).Select(p => Icons.Get(p.Path))).Concat(model.Catalog.Apps.Take(30).Select(a => Icons.Get(a.Path))));
                var monitor = model.CurrentMonitor();
                var launcher = new Launcher(model, monitor); launcher.PreparePreview(); Save(launcher, "launcher");
                launcher.PreparePreview("notepad"); Save(launcher, "search"); launcher.Close();
                var settings = new SettingsWindow(model); Save(settings, "settings"); settings.Close();
                var quick = new QuickPanel(model, monitor); Save(quick, "quick-settings"); quick.Close();
                var calendar = new CalendarPanel(monitor); Save(calendar, "calendar"); calendar.Close();
                var arrange = new ArrangePanel(model, monitor); Save(arrange, "layouts"); arrange.Close();
                var mods = new ModCatalog(model); Save(mods, "mods"); mods.Close();
                foreach (string category in ModTaxonomy.Main) { var showcase = new ModCatalog(model); showcase.PreparePreview("category:" + category); Save(showcase, "category-" + category.ToLowerInvariant()); showcase.Close(); }
                var helpCatalog = new ModCatalog(model); helpCatalog.PreparePreview("detail:harbor.calculator"); Save(helpCatalog, "plugin-guide"); helpCatalog.Close();
                var largePreview = new ModPreviewWindow(model, model.Mods.Catalog.First(m => m.Id == "harbor.calculator")); Save(largePreview, "large-preview"); largePreview.Close();
                var catalog = new ModCatalog(model); catalog.PreparePreview("catalog:Tools"); Save(catalog, "catalog-tools"); catalog.Close();
                var movie = new ModBanner(model.Mods.Catalog.First(m => m.Id == "harbor.window-opacity"), model, 400); movie.PreparePreviewFrame(70); var filmWindow = new Window { Width = 720, Height = 400, Content = movie }; Save(filmWindow, "preview-frame"); filmWindow.Close();
                var creator = new ModCreator(model); Save(creator, "plugin-creator"); creator.Close();
                var behaviors = new ModCatalog(model); behaviors.PreparePreview("Behavior mods"); Save(behaviors, "behavior-mods"); behaviors.Close();
                var profiles = new ProfileWindow(model); Save(profiles, "profiles"); profiles.Close();
                var monitorBounds = Native.Monitor(monitor).Monitor; var dockControls = new DockControls(model, monitor, monitorBounds); Save(dockControls, "dock-controls"); dockControls.Close();
                foreach (string page in new[] { "Buttons", "Style", "Behavior" }) { var topControls = new TopDockControls(model, monitor); topControls.PreparePreview(page); Save(topControls, "top-dock-" + page.ToLowerInvariant()); topControls.Close(); }
                var topBar = new TopBar(model, monitor); topBar.Width = 1240; topBar.PreparePreview(); Save(topBar, "top-dock"); topBar.Close();
                var savedPieces = JsonSerializer.Serialize(model.Config.TopDockItems); foreach (var piece in model.Config.TopDockItems) piece.Position = null; BarLayout.Move(model.Config, "play", "time", "center");
                var composed = new TopBar(model, monitor); composed.Width = 1240; composed.PreparePreview(); Save(composed, "top-dock-composed"); composed.Close();
                var pieceEditor = new DockPieceEditor(model, model.Config.TopDockItems.First(i => i.Id == "time")); Save(pieceEditor, "dock-piece-editor"); pieceEditor.Close(); model.Config.TopDockItems = JsonSerializer.Deserialize<List<BarItem>>(savedPieces)!;
                var folderDemo = new AppFolder { Name = "Favorites", Apps = model.Config.Pins.Where(p => !TaskbarFolders.IsFolder(p.Path)).Take(4).ToList() }; var savedPins = model.Config.Pins.ToList();
                model.Config.AppFolders.Add(folderDemo); model.Config.Pins.RemoveAll(p => folderDemo.Apps.Contains(p)); model.Config.Pins.Add(new Pin { Name = folderDemo.Name, Path = TaskbarFolders.Path(folderDemo), Target = TaskbarFolders.Path(folderDemo) });
                var folderPopup = new AppFolderPopup(model, folderDemo, monitor); Save(folderPopup, "app-folder"); folderPopup.Close();
                var appFiles = new Files(model, TaskbarFolders.Path(folderDemo)); Save(appFiles, "files-app-folder"); appFiles.Close();
                var folderDock = new Dock(model, monitor); folderDock.Refresh(true); Save(folderDock, "taskbar-folder"); folderDock.Close(); model.Config.AppFolders.Remove(folderDemo); model.Config.Pins.Clear(); model.Config.Pins.AddRange(savedPins);
                var menuDock = new Dock(model, monitor); var dockMenu = menuDock.ContextMenu!; dockMenu.Visibility = Visibility.Visible; dockMenu.ApplyTemplate();
                dockMenu.Measure(new Size(248, 190)); dockMenu.Arrange(new Rect(0, 0, 248, 190)); dockMenu.UpdateLayout();
                var menuTarget = new RenderTargetBitmap(248, 190, 96, 96, PixelFormats.Pbgra32); menuTarget.Render(dockMenu); var menuEncoder = new PngBitmapEncoder(); menuEncoder.Frames.Add(BitmapFrame.Create(menuTarget)); using (var menuFile = File.Create(Path.Combine(directory, "dock-menu.png"))) menuEncoder.Save(menuFile); menuDock.Close();
                string sample = Path.Combine(directory, "sample-files"); Directory.CreateDirectory(Path.Combine(sample, "Projects")); Directory.CreateDirectory(Path.Combine(sample, "Designs")); File.WriteAllText(Path.Combine(sample, "Welcome.txt"), "Welcome to Harbor Files.\n\nUse tabs to keep folders open.\nSwitch between a grid and details.\nPreview images and text here.\nPin folders you use often."); File.WriteAllText(Path.Combine(sample, "palette.json"), "{\n  \"accent\": \"#83DFD0\",\n  \"theme\": \"Midnight\"\n}"); File.WriteAllText(Path.Combine(sample, "Notes.md"), "# Ideas\nA desktop that works your way.");
                var files = new Files(model, sample); await files.NavigateAsync(sample, false); files.PrepareDetailsPreview(); files.PreparePreview("Welcome.txt"); await Task.Delay(100); Save(files, "files"); string oldView = model.Config.FilesView; files.PrepareGridPreview(); Save(files, "files-grid"); files.PrepareOverviewPreview(); Save(files, "files-overview"); model.Config.FilesView = oldView; files.Close();
                var homeFiles = new Files(model); Save(homeFiles, "files-home"); homeFiles.Close();
                var desktop = new Desktop(model, monitor, true); Save(desktop, "desktop"); desktop.Close();
                var appearance = new Appearance(model); Save(appearance, "appearance"); appearance.Close();
                var dashboard = model.Mods.Catalog.First(m => m.Component == "dashboard"); var designer = new ModDesigner(model, dashboard); Save(designer, "builder"); designer.Close();
                var card = new Widget(model, model.Mods.Catalog.First(m => m.Id == "harbor.resource-desk"), true); card.View.Measure(new Size(360, double.PositiveInfinity)); card.View.Arrange(new Rect(0, 0, 360, card.View.DesiredSize.Height));
                var dashboardWindow = new Window { Width = 380, Height = Math.Max(300, card.View.DesiredSize.Height + 24), Content = new Border { Background = Theme.Bg, Padding = new Thickness(10), Child = card.View } }; Save(dashboardWindow, "dashboard"); dashboardWindow.Close();
                File.WriteAllText(Path.Combine(directory, "visual-check.json"), JsonSerializer.Serialize(new { RenderMode = RenderOptions.ProcessRenderMode.ToString(), model.Catalog.Apps.Count, ModCount = model.Mods.Catalog.Count, Images = 36, DesktopChanged = false }));
                app.Shutdown();
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); app.Shutdown(1); }
        };
        model.Catalog.Start();
        app.Run();
        void Save(Window window, string name)
        {
            window.ApplyTemplate();
            var content = (FrameworkElement)window.Content;
            var size = new Size(window.Width, window.Height);
            content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
            var target = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            target.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
        }
    }
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        int index = 0;
        foreach (var pin in Settings.Load().Pins)
        {
            var image = Icons.Get(pin.Path).GetAwaiter().GetResult();
            bool ok = image is BitmapSource;
            if (image is BitmapSource bitmap)
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, "icon-" + index + ".png")); encoder.Save(file);
            }
            checks.Add(new { pin.Name, IconLoaded = ok }); index++;
        }
        // Exercise shell-image extraction for packaged apps independently of the visible UI.
        var packaged = Icons.Get(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App").GetAwaiter().GetResult();
        checks.Add(new { Name = "Packaged app icon", IconLoaded = packaged != null });
        var ownPath = Native.ProcessPath((uint)Environment.ProcessId);
        bool pathOk = ownPath.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(Path.Combine(directory, "diagnostics.json"), JsonSerializer.Serialize(new { Checks = checks, ProcessPathWorks = pathOk, RenderMode = RenderOptions.ProcessRenderMode.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
