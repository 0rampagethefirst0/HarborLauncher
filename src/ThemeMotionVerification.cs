using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class ThemeMotionVerification
{
    internal static void Run(Shell shell, Action<string, bool, object?> check)
    {
        void C(string name, bool passed, object? detail = null) => check(name, passed, detail);
        byte[] Pixels(FrameworkElement view)
        {
            int width = (int)view.Width, height = (int)view.Height; view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0); return pixels;
        }
        byte[] Image(BitmapSource bitmap) { var p = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(p, bitmap.PixelWidth * 4, 0); return p; }
        string oldSkin = Theme.Skin;
        try
        {
            var required = new[] { "background", "panel", "accent", "text", "muted", "raised", "border", "wallpaper", "logo", "fire" };
            C("Every preset theme exposes surface, text, wallpaper, logo and fire colors", shell.Mods.Catalog.Where(m => m.Kind == "theme" && m.Id.StartsWith("harbor.")).All(m => required.All(k => m.Options.Any(o => o.Key == k && o.Type == "color"))));
            Theme.SetSkin("harbor"); Theme.Palette("#111D1A", "#1A2C25", "#84D4AC", "#EDF1FA"); var greenLogo = Pixels(new HarborEmblem(180)); var greenWallpaper = Image(HarborBackdrop.Image(320, 200));
            Theme.Palette("#211715", "#342420", "#FFB08A", "#EDF1FA"); var warmLogo = Pixels(new HarborEmblem(180)); var warmWallpaper = Image(HarborBackdrop.Image(320, 200));
            C("Changing a theme recolors both the 3D logo and the cached wallpaper", !greenLogo.SequenceEqual(warmLogo) && !greenWallpaper.SequenceEqual(warmWallpaper));
            Theme.Palette("#211715", "#342420", "#FFB08A", "#EDF1FA", wallpaper: "#0E1028", logo: "#AF8AFF", fire: "#70F5ED");
            C("Wallpaper, logo and fire can be colored independently of UI accent", Theme.CurrentColors.Accent != Theme.CurrentColors.Logo && Theme.CurrentColors.Logo != Theme.CurrentColors.Fire && !Image(HarborBackdrop.Image(320, 200)).SequenceEqual(warmWallpaper));
            Theme.SetSkin("tokyo-dragon"); var front = Pixels(new HarborEmblem(200) { Turn = 0 }); var side = Pixels(new HarborEmblem(200) { Turn = 75 });
            C("The outline dragon changes perspective during a real 3D rotation", !front.SequenceEqual(side) && (DragonArt.Project(new Point(150, 100), 4, 200, 200, 65) - DragonArt.Project(new Point(150, 100), -4, 200, 200, 65)).Length > 1);
            C("The fire performance tilts back and fades fully at both endpoints", DragonHomeFire.Tilt(.4) < -15 && DragonHomeFire.ActorOpacity(0) == 0 && DragonHomeFire.ActorOpacity(1) == 0 && DragonHomeFire.FireOpacity(0) == 0 && DragonHomeFire.FireOpacity(1) == 0 && DragonHomeFire.IdleOpacity(0) == 1);
            var fire = new DragonHomeFire { Width = 800, Height = 500, Origin = new Point(400, 250), LogoSize = 180, Progress = 0 }; var empty = Pixels(fire); fire.Progress = .48; var spray = Pixels(fire); fire.Progress = .72; var spread = Pixels(fire); fire.Progress = 1;
            C("The flame changes shape and settles into Home's actual surface", !empty.SequenceEqual(spray) && !spray.SequenceEqual(spread) && !Pixels(fire).SequenceEqual(empty) && fire.Surface.Bounds == new Rect(45, 35, 710, 430));
            fire.Progress = .44; double roll = DragonHomeFire.Roll(.44) - DragonArt.ProfileHeading; var eye = DragonArt.Project(new Point(105, 81), 4, 224, 224, 0, rollDegrees: roll); var muzzle = DragonArt.Project(new Point(172, 105), 4, 224, 224, 0, rollDegrees: roll);
            C("The breathing pose and flame direction use a seventeen-degree side profile", Math.Abs(Math.Atan2(muzzle.Y - eye.Y, muzzle.X - eye.X) * 180 / Math.PI + 17) < .001 && Math.Abs(Math.Atan2(fire.Direction.Y, fire.Direction.X) * 180 / Math.PI + 17) < .0001);
            fire.Progress = .73; var clip = fire.ContentClip(new Point(45, 35)); var surface = fire.Surface.Bounds; C("Home content uses the same changing flame contour as its surface", Math.Abs(clip.Bounds.Left - (surface.Left - 45)) < .001 && Math.Abs(clip.Bounds.Top - (surface.Top - 35)) < .001 && Math.Abs(clip.Bounds.Width - surface.Width) < .001);
            fire.HomeBounds = new Rect(45, 35, 710, 430); var cached = DragonFrameCache.Get(fire).GetAwaiter().GetResult(); var sample = cached.At(.78);
            C("Background dragon frames are materialized and content waits for the completed Home contour", cached.Items.Length >= 150 && cached.Items.Where(f => f.Image != null).All(f => f.Image!.IsFrozen && f.Image is not RenderTargetBitmap) && DragonHomeFire.ContentReveal(.95) == 0 && DragonHomeFire.ContentReveal(1) == 1 && Math.Abs(sample.Progress - .78) < .012);
            var monitor = shell.CurrentMonitor(); var work = PluginSnapping.WorkArea(shell, monitor); double dpi = Native.Scale(monitor); var saved = new EmblemState(new Rect(work.Left + 340 * dpi, work.Top + 250 * dpi, 210 * dpi, 210 * dpi), 137.25);
            var home = new HarborHome(shell, monitor, saved, demo: true); home.PreparePreview(.72); home.PreparePreview(0); var returned = home.FlyingState; var expected = saved.RelativeTo(new Point(work.Left, work.Top), dpi);
            C("The dragon returns to its saved desktop bounds and rotation after the fire pose", Math.Abs(returned.Turn - saved.Turn) < .0001 && (returned.Center - expected.Center).Length < .001 && Math.Abs(returned.Bounds.Width - expected.Bounds.Width) < .001); home.Close();
            Exception? crossThread = null; var worker = new Thread(() => { try { Pixels(new HarborEmblem(100)); HarborBackdrop.Image(320, 200); } catch (Exception ex) { crossThread = ex; } }) { IsBackground = true }; worker.SetApartmentState(ApartmentState.STA); worker.Start(); bool done = worker.Join(5000);
            C("The startup dispatcher can render the themed dragon without cross-thread brushes", done && crossThread == null, crossThread?.ToString());
            var settings = new Settings { TransparencyVersion = 1, TaskbarBaseTransparency = .5, TaskbarHoverTransparency = 99.5, TaskbarActiveTransparency = 17.5, DockInteractionDelay = 500, TopDockOpacity = .005 }; settings.Normalize();
            C("Half-step visual values survive normalization without truncation", settings.TaskbarBaseTransparency == .5 && settings.TaskbarHoverTransparency == 99.5 && settings.TaskbarActiveTransparency == 17.5 && settings.DockInteractionDelay == 500 && settings.TopDockOpacity == .005 && Math.Abs(TaskbarTransparency.Opacity(settings, TaskbarStage.Hover) - .005) < .0001);
        }
        finally { Theme.SetSkin(oldSkin); shell.Mods.ApplyTheme(); }
    }
}
