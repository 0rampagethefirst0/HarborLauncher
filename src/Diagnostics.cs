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
        foreach (var mod in model.Mods.Catalog.Where(m => visualSettings.EnabledMods.Contains(m.Id)).ToArray()) model.Mods.Install(mod, false);
        ShellFeatures.Sync(visualSettings, model.Mods);
        model.Catalog.Changed += async () =>
        {
            try
            {
                await Task.WhenAll(model.Config.Pins.Select(p => Icons.Get(p.Path)).Concat(model.Config.AppFolders.SelectMany(f => f.Apps).Select(p => Icons.Get(p.Path))).Concat(model.Catalog.Apps.Take(30).Select(a => Icons.Get(a.Path))));
                var monitor = model.CurrentMonitor();
                var backdrop = new Window { Width = 1280, Height = 720, Background = HarborBackdrop.Brush(1280, 720), Content = new Grid { Background = HarborBackdrop.Brush(1280, 720), Children = { new HarborEmblem(240) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } } }; Save(backdrop, "harbor-background"); backdrop.Close();
                foreach (double angle in new[] { -16d, 55d, 145d }) { var mark = new HarborEmblem(300) { Turn = angle }; var markWindow = new Window { Width = 420, Height = 420, Background = Theme.Brush("#060C17"), Content = new Border { Background = Theme.Brush("#060C17"), Child = mark } }; Save(markWindow, "emblem-" + ((int)angle).ToString()); markWindow.Close(); }
                foreach (double phase in new[] { .18, .5, 1d }) { var artwork = new StartupArtwork { Progress = phase }; var movieWindow = new Window { Width = 1280, Height = 720, Content = artwork }; Save(movieWindow, "startup-" + (phase * 100).ToString("0")); movieWindow.Close(); }
                foreach (double phase in new[] { .2, .55, 1d }) { var home = new HarborHome(model, monitor, demo: true); home.PreparePreview(phase); Save(home, "home-" + (phase * 100).ToString("0")); home.Close(); }
                foreach (string page in new[] { "Layout", "Tabs", "Details" }) { var homeSettings = new HomeSettings(model); homeSettings.PreparePreview(page); Save(homeSettings, "home-settings-" + page.ToLowerInvariant()); homeSettings.Close(); }
                string priorSkin = Theme.Skin; Theme.SetSkin("tokyo-dragon"); Theme.Palette("#0C0C10", "#171216", "#F33D4F", "#F7ECEE", "#B99CA3", "#2A1B22", "#57313C", fire: "#FF8055");
                var dragonHome = new HarborHome(model, monitor, demo: true); dragonHome.PreparePreview(); Save(dragonHome, "tokyo-dragon-home"); dragonHome.Close();
                foreach (double phase in new[] { .20, .35, .50, .65, .78, .90, .98 }) { var fireHome = new HarborHome(model, monitor, demo: true); fireHome.PreparePreview(phase); var body = (FrameworkElement)fireHome.Content; fireHome.Content = null; var fireBackdrop = new Grid { Background = fireHome.Background }; fireBackdrop.Children.Add(body); var review = new Window { Width = fireHome.Width, Height = fireHome.Height, Content = fireBackdrop }; Save(review, "dragon-fire-" + (phase * 100).ToString("0")); review.Close(); fireHome.Close(); }
                foreach (double turn in new[] { 0d, 55d, 140d, 240d }) { var mark = new HarborEmblem(240) { Turn = turn }; var logoWindow = new Window { Width = 340, Height = 340, Content = mark, Background = Theme.Bg }; Save(logoWindow, "dragon-turn-" + turn.ToString("0")); logoWindow.Close(); }
                var dragonDeskRoot = new Grid { Background = HarborBackdrop.Brush(1280, 900) }; dragonDeskRoot.Children.Add(new HarborEmblem(240) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }); dragonDeskRoot.Children.Add(new DesktopBin(model));
                var dragonDesk = new Window { Width = 1280, Height = 900, Content = dragonDeskRoot }; Save(dragonDesk, "tokyo-dragon-desktop"); dragonDesk.Close();
                Theme.SetSkin(priorSkin); model.Mods.ApplyTheme();
                var binFiles = new Files(model, RecycleBin.Location); await binFiles.NavigateAsync(RecycleBin.Location, false); binFiles.PrepareRecyclePreview(); Save(binFiles, "files-recycle-bin"); binFiles.Close();
                var originalMenu = model.Plugins.Menu(model.Mods.Catalog.First(m => m.Id == "harbor.system"), monitor); originalMenu.Visibility = Visibility.Visible; originalMenu.ApplyTemplate(); originalMenu.Measure(new Size(330, 620)); originalMenu.Arrange(new Rect(0, 0, 330, 620)); originalMenu.UpdateLayout();
                var originalTarget = new RenderTargetBitmap(330, 620, 96, 96, PixelFormats.Pbgra32); originalTarget.Render(originalMenu); var originalEncoder = new PngBitmapEncoder(); originalEncoder.Frames.Add(BitmapFrame.Create(originalTarget)); using (var originalFile = File.Create(Path.Combine(directory, "plugin-right-click.png"))) originalEncoder.Save(originalFile);
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
                var monitorBounds = Native.Monitor(monitor).Monitor; var dockControls = new DockControls(model, monitor, monitorBounds); Save(dockControls, "dock-controls"); foreach (string page in new[] { "Interaction", "Apps" }) { dockControls.PreparePreview(page); Save(dockControls, "taskbar-controls-" + page.ToLowerInvariant()); } dockControls.Close();
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
                var accessExamples = new StackPanel { Margin = new Thickness(28), Width = 450 }; accessExamples.Children.Add(Theme.Label("Tools within reach", 28)); accessExamples.Children.Add(new TextBlock { Text = "Small independent controls. Click to expand; click away to collapse. Right-click to choose edge, floating, Dock or desktop access.", FontSize = 15, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 24) });
                foreach (string id in new[] { "clock", "system", "folders", "storage", "resource-desk" }) { var face = PluginHandle.Face(model, model.Mods.Catalog.First(m => m.Id == "harbor." + id), 112, () => { }, out _, out _, true); face.Width = 112; face.Height = 36; face.Margin = new Thickness(0, 0, 0, 8); face.HorizontalAlignment = HorizontalAlignment.Left; accessExamples.Children.Add(face); }
                var accessWindow = new Window { Width = 540, Height = 500, Background = Theme.Bg, Content = accessExamples }; Save(accessWindow, "plugin-controls"); accessWindow.Close();
                var snapView = new SnapGuides(model, monitor); var snapCanvas = (Canvas)snapView.Content; var snapArea = new Rect(0, 0, snapView.Width, snapView.Height);
                foreach (string id in new[] { "clock", "system", "folders", "storage", "resource-desk" }) { var mod = model.Mods.Catalog.First(m => m.Id == "harbor." + id); double width = id is "system" or "resource-desk" ? 192 : id == "storage" ? 224 : 112; var face = PluginHandle.Face(model, mod, width, () => { }, out _, out _, true); face.Width = width; face.Height = 36; var position = PluginSnapping.Place(snapArea, new Size(width, 36), PluginSnapping.Default(mod), .9, .5); Canvas.SetLeft(face, position.Left); Canvas.SetTop(face, position.Top); snapCanvas.Children.Add(face); }
                Save(snapView, "snap-layout"); snapView.Close();
                var readingEditor = new PluginReadingEditor(model, model.Mods.Catalog.First(m => m.Id == "harbor.system")); Save(readingEditor, "reading-editor"); readingEditor.Close();
                var statsMod = model.Mods.Catalog.First(m => m.Id == "harbor.system");
                foreach (string section in new[] { "Visibility", "Position", "Appearance" }) { var controlSettings = new PluginQuickSettings(model, statsMod, monitor); controlSettings.PreparePreview(section); Save(controlSettings, "plugin-settings-" + section.ToLowerInvariant()); controlSettings.Close(); }
                var priorOptions = JsonSerializer.Serialize(model.Config.ModOptions);
                model.Mods.Set(statsMod, "visibility", PluginVisibility.Selected, false); model.Mods.Set(statsMod, "visible-apps", "Discord.exe\nnotepad.exe", false); model.Mods.Set(statsMod, "always-on", "Yes", false);
                var appRuleSettings = new PluginQuickSettings(model, statsMod, monitor); Save(appRuleSettings, "plugin-settings-apps"); appRuleSettings.Close(); model.Config.ModOptions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(priorOptions)!;
                var dividerEditor = new DividerEditor(model, "Divider settings", () => 32, _ => { }, () => 2, _ => { }, () => 26, _ => { }, () => false, _ => { }, _ => { }, () => { }); Save(dividerEditor, "divider-editor"); dividerEditor.Close();
                var savedFeatures = model.Config.EnabledMods.ToList(); model.Config.EnabledMods.RemoveAll(id => model.Mods.Catalog.Any(m => m.Kind == "feature" && m.Id == id)); ShellFeatures.Sync(model.Config, model.Mods); var featureCatalog = new ModCatalog(model); featureCatalog.ShowFeatures(); Save(featureCatalog, "features-default-off"); featureCatalog.Close(); model.Config.EnabledMods = savedFeatures; ShellFeatures.Sync(model.Config, model.Mods);
                foreach (string id in new[] { "clock", "system", "folders", "storage", "notes", "focus", "resource-desk", "day-planner" }) { var mod = model.Mods.Catalog.First(m => m.Id == "harbor." + id); var tool = new Widget(model, mod, true, new WidgetDemoData(), true); var toolPopup = new PluginPopup(model, mod, tool, monitor); Save(toolPopup, "plugin-" + id); toolPopup.RequestClose(); }
                var appearance = new Appearance(model); Save(appearance, "appearance"); appearance.Close();
                var dashboard = model.Mods.Catalog.First(m => m.Component == "dashboard"); var designer = new ModDesigner(model, dashboard); Save(designer, "builder"); designer.Close();
                var card = new Widget(model, model.Mods.Catalog.First(m => m.Id == "harbor.resource-desk"), true); card.View.Measure(new Size(360, double.PositiveInfinity)); card.View.Arrange(new Rect(0, 0, 360, card.View.DesiredSize.Height));
                var dashboardWindow = new Window { Width = 380, Height = Math.Max(300, card.View.DesiredSize.Height + 24), Content = new Border { Background = Theme.Bg, Padding = new Thickness(10), Child = card.View } }; Save(dashboardWindow, "dashboard"); dashboardWindow.Close();
                File.WriteAllText(Path.Combine(directory, "visual-check.json"), JsonSerializer.Serialize(new { RenderMode = RenderOptions.ProcessRenderMode.ToString(), model.Catalog.Apps.Count, ModCount = model.Mods.Catalog.Count, Images = Directory.GetFiles(directory, "*.png").Length, DesktopChanged = false }));
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
