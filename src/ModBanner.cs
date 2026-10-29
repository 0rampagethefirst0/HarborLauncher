using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Harbor;

internal static class CatalogMotion
{
    internal static void Enter(FrameworkElement element, Settings config)
    {
        element.Loaded += (_, _) => {
            if (config.DockMotionMilliseconds == 0 || Window.GetWindow(element)?.IsActive != true) return;
            var move = new TranslateTransform(); element.RenderTransform = move;
            var duration = TimeSpan.FromMilliseconds(Math.Max(140, config.DockMotionMilliseconds));
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
    }
    internal static void Hover(FrameworkElement element, Settings config)
    {
        var move = new TranslateTransform(); element.RenderTransform = move;
        void To(double value) { if (config.DockMotionMilliseconds > 0) move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(value, TimeSpan.FromMilliseconds(config.DockMotionMilliseconds)) { EasingFunction = new QuadraticEase() }); }
        element.MouseEnter += (_, _) => To(-3); element.MouseLeave += (_, _) => To(0);
    }
}

internal sealed class ModBanner : Border
{
    static ModBanner? playing;
    readonly Image image = new() { Stretch = Stretch.Uniform };
    readonly Settings config;
    readonly ModDefinition mod;
    readonly bool autoplay;
    readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromMilliseconds(ModDemoScene.FrameMilliseconds) };
    readonly Stopwatch playback = new();
    readonly TextBlock playLabel = Theme.Label("Hover to play", 10, Theme.Brush("#E5EFFD"));
    BitmapSource? poster, film;
    Window? owner;
    int frame;
    bool custom;
    internal static Brush Accent(string main) => Theme.Brush(main == "Desktop" ? "#83DFD0" : main == "Themes" ? "#C6ACFF" : "#FFC593");
    internal ModBanner(ModDefinition mod, Shell shell, double height = 162, bool autoplay = false)
    {
        this.mod = mod; config = shell.Config; this.autoplay = autoplay;
        Height = height; CornerRadius = new CornerRadius(12); ClipToBounds = true; Background = Theme.Brush("#101722");
        var root = new Grid(); root.Children.Add(image); Child = root;
        if (mod.PreviewImage.Length > 0)
        {
            try { using var input = new MemoryStream(Convert.FromBase64String(mod.PreviewImage[(mod.PreviewImage.IndexOf(',') + 1)..])); poster = Decode(input); custom = true; } catch (Exception ex) { Log.Write("Banner image: " + ex.Message); }
        }
        poster ??= Load(mod.Id + ".png");
        if (poster == null)
        {
            // New/custom packages get their own real layout, not another package's picture.
            try { var demo = new ModDemoScene(shell, mod); poster = ModDemoRecorder.Snapshot(demo.View); }
            catch (Exception ex) { Log.Write("Native preview: " + ex.Message); }
        }
        image.Source = poster;
        if (poster == null) root.Children.Add(Theme.Label("Preview not supplied", 13, Theme.Muted));
        if (!custom && mod.Id.StartsWith("harbor."))
        {
            var badge = new Border { Background = Theme.Brush("#DB172433"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6), Child = playLabel, IsHitTestVisible = false }; root.Children.Add(badge);
        }
        clock.Tick += (_, _) => {
            if (!IsVisible || owner?.IsActive != true || config.DockMotionMilliseconds == 0) { Stop(); return; }
            clock.Interval = TimeSpan.FromMilliseconds(1000d / config.PreviewFramesPerSecond);
            int current = (int)(playback.Elapsed.TotalMilliseconds / ModDemoScene.FrameMilliseconds);
            if (current >= ModDemoScene.Frames + 30) { if (!autoplay) { Stop(); return; } playback.Restart(); current = 0; }
            current = Math.Min(current, ModDemoScene.Frames - 1); if (current != frame) { frame = current; SetFrame(frame); }
        };
        MouseEnter += (_, _) => Start(); MouseLeave += (_, _) => { if (!autoplay) Stop(); };
        Loaded += (_, _) => { owner = Window.GetWindow(this); if (owner != null) { owner.Activated += Active; owner.Deactivated += Inactive; } if (autoplay) Start(); };
        Unloaded += (_, _) => { Stop(); if (owner != null) { owner.Activated -= Active; owner.Deactivated -= Inactive; owner = null; } };
    }
    void Active(object? sender, EventArgs e) { if (autoplay || IsMouseOver) Start(); }
    void Inactive(object? sender, EventArgs e) => Stop();
    void Start()
    {
        if (custom || config.DockMotionMilliseconds == 0 || owner?.IsActive != true || !IsVisible) return;
        if (clock.IsEnabled) return;
        playing?.Stop(); film = Load(mod.Id + "-film.png"); if (film == null) return;
        playing = this; frame = 0; playLabel.Text = "Preview playing"; SetFrame(0); playback.Restart(); clock.Interval = TimeSpan.FromMilliseconds(1000d / config.PreviewFramesPerSecond); clock.Start();
    }
    void SetFrame(int index)
    {
        if (film == null) return;
        var crop = new CroppedBitmap(film, new Int32Rect(index % ModDemoScene.Columns * ModDemoScene.FilmWidth, index / ModDemoScene.Columns * ModDemoScene.FilmHeight, ModDemoScene.FilmWidth, ModDemoScene.FilmHeight)); crop.Freeze(); image.Source = crop;
    }
    internal void PreparePreviewFrame(int index) { film = Load(mod.Id + "-film.png"); SetFrame(Math.Clamp(index, 0, ModDemoScene.Frames - 1)); }
    void Stop() { clock.Stop(); playback.Stop(); image.Source = poster; film = null; playLabel.Text = "Hover to play"; if (ReferenceEquals(playing, this)) playing = null; }
    static BitmapSource? Load(string name)
    {
        try { var info = Application.GetResourceStream(new Uri("pack://application:,,,/Harbor;component/Previews/" + name)); if (info == null) return null; using (info.Stream) return Decode(info.Stream, false); } catch (IOException) { return null; }
    }
    static BitmapSource Decode(Stream input, bool limit = true) { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; if (limit) bitmap.DecodePixelWidth = 1200; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
}

