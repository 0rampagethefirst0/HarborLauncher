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
        var model = new Shell(Settings.Load());
        model.Catalog.Changed += async () =>
        {
            try
            {
                await Task.WhenAll(model.Config.Pins.Select(p => Icons.Get(p.Path)).Concat(model.Catalog.Apps.Take(30).Select(a => Icons.Get(a.Path))));
                var monitor = model.CurrentMonitor();
                var launcher = new Launcher(model, monitor); launcher.PreparePreview(); Save(launcher, "launcher");
                launcher.PreparePreview("notepad"); Save(launcher, "search"); launcher.Close();
                var settings = new SettingsWindow(model); Save(settings, "settings"); settings.Close();
                var quick = new QuickPanel(model, monitor); Save(quick, "quick-settings"); quick.Close();
                var calendar = new CalendarPanel(monitor); Save(calendar, "calendar"); calendar.Close();
                var arrange = new ArrangePanel(model, monitor); Save(arrange, "layouts"); arrange.Close();
                var mods = new ModCatalog(model); Save(mods, "mods"); mods.Close();
                foreach (string category in ModTaxonomy.Main) { var showcase = new ModCatalog(model); showcase.PreparePreview("category:" + category); Save(showcase, "category-" + category.ToLowerInvariant()); showcase.Close(); }
                var catalog = new ModCatalog(model); catalog.PreparePreview("catalog:Tools"); Save(catalog, "catalog-tools"); catalog.Close();
                var creator = new ModCreator(model); Save(creator, "plugin-creator"); creator.Close();
                var behaviors = new ModCatalog(model); behaviors.PreparePreview("Behavior mods"); Save(behaviors, "behavior-mods"); behaviors.Close();
                var profiles = new ProfileWindow(model); Save(profiles, "profiles"); profiles.Close();
                var monitorBounds = Native.Monitor(monitor).Monitor; var dockControls = new DockControls(model, monitor, monitorBounds); Save(dockControls, "dock-controls"); dockControls.Close();
                var menuDock = new Dock(model, monitor); var dockMenu = menuDock.ContextMenu!; dockMenu.Visibility = Visibility.Visible; dockMenu.ApplyTemplate();
                dockMenu.Measure(new Size(248, 190)); dockMenu.Arrange(new Rect(0, 0, 248, 190)); dockMenu.UpdateLayout();
                var menuTarget = new RenderTargetBitmap(248, 190, 96, 96, PixelFormats.Pbgra32); menuTarget.Render(dockMenu); var menuEncoder = new PngBitmapEncoder(); menuEncoder.Frames.Add(BitmapFrame.Create(menuTarget)); using (var menuFile = File.Create(Path.Combine(directory, "dock-menu.png"))) menuEncoder.Save(menuFile); menuDock.Close();
                string sample = Path.Combine(directory, "sample-files"); Directory.CreateDirectory(Path.Combine(sample, "Projects")); File.WriteAllText(Path.Combine(sample, "Welcome.txt"), "Harbor Files");
                var files = new Files(model, sample); await Task.Delay(250); Save(files, "files"); files.Close();
                var desktop = new Desktop(model, monitor, true); Save(desktop, "desktop"); desktop.Close();
                var appearance = new Appearance(model); Save(appearance, "appearance"); appearance.Close();
                var dashboard = model.Mods.Catalog.First(m => m.Component == "dashboard"); var designer = new ModDesigner(model, dashboard); Save(designer, "builder"); designer.Close();
                var card = new Widget(model, model.Mods.Catalog.First(m => m.Id == "harbor.resource-desk"), true); card.View.Measure(new Size(360, double.PositiveInfinity)); card.View.Arrange(new Rect(0, 0, 360, card.View.DesiredSize.Height));
                var dashboardWindow = new Window { Width = 380, Height = Math.Max(300, card.View.DesiredSize.Height + 24), Content = new Border { Background = Theme.Bg, Padding = new Thickness(10), Child = card.View } }; Save(dashboardWindow, "dashboard"); dashboardWindow.Close();
                File.WriteAllText(Path.Combine(directory, "visual-check.json"), JsonSerializer.Serialize(new { RenderMode = RenderOptions.ProcessRenderMode.ToString(), model.Catalog.Apps.Count, ModCount = model.Mods.Catalog.Count, Images = 21, DesktopChanged = false }));
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
