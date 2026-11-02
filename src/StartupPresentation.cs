using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Harbor;

internal sealed class StartupArtwork : Grid
{
    readonly HarborEmblem emblem;
    readonly ScaleTransform scale = new();
    readonly TextBlock word, status;
    readonly Border line;
    readonly Grid branding;
    internal int RenderedFrames => emblem.RenderedFrames;
    internal double RenderedFPS => emblem.RenderedFPS;
    internal object FramePacing => emblem.FramePacing;
    internal static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(StartupArtwork), new PropertyMetadata(0d, (d, _) => ((StartupArtwork)d).Paint()));
    internal double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    internal StartupArtwork(int logoSize = 240)
    {
        Background = HarborBackdrop.Brush(1600, 900);
        emblem = new HarborEmblem(logoSize) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5), RenderTransform = scale, SampleFrames = true }; Children.Add(emblem);
        branding = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, logoSize + 120, 0, 0) }; var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        word = Text("H A R B O R", 23, Theme.CurrentColors.Text.ToString()); word.FontWeight = FontWeights.Light; stack.Children.Add(word);
        var accent = Theme.CurrentColors.Accent; var clear = accent; clear.A = 0; line = new Border { Height = 1, Width = 140, Margin = new Thickness(0, 22, 0, 18), Background = new LinearGradientBrush(clear, accent, 0) }; stack.Children.Add(line);
        status = Text("Preparing your desktop", 12, Theme.CurrentColors.Muted.ToString()); stack.Children.Add(status); branding.Children.Add(stack); Children.Add(branding); Paint();
    }
    static TextBlock Text(string text, double size, string color) => new() { Text = text, FontFamily = new FontFamily("Segoe UI"), FontSize = size, Foreground = Frozen(color), HorizontalAlignment = HorizontalAlignment.Center };
    static Brush Frozen(string color) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); b.Freeze(); return b; }
    internal void Stage(string value) => status.Text = value;
    internal void FadeOut(int fps, Action done)
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(450)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Timeline.SetDesiredFrameRate(fade, fps); fade.Completed += (_, _) => done();
        // Fade the small artwork, not millions of background pixels.
        emblem.BeginAnimation(UIElement.OpacityProperty, fade);
        var words = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350)); Timeline.SetDesiredFrameRate(words, fps); branding.BeginAnimation(UIElement.OpacityProperty, words);
    }
    void Paint()
    {
        if (emblem == null) return; double p = Math.Clamp(Progress, 0, 1), ease = 1 - Math.Pow(1 - p, 3);
        emblem.Turn = -126 + 470 * ease; emblem.Opacity = Math.Clamp(p * 4, 0, 1); scale.ScaleX = scale.ScaleY = .62 + .38 * ease;
        branding.Opacity = Math.Clamp((p - .18) * 2.4, 0, 1); line.Width = 30 + 110 * ease;
    }
}

// An independent UI dispatcher keeps the intro smooth while the shell is built.
// The artwork owns its brushes; it never touches the main thread's theme objects.
internal sealed class StartupPresentation : IDisposable
{
    readonly Thread thread;
    readonly Settings config;
    readonly bool probe;
    Dispatcher? dispatcher;
    readonly List<Window> windows = new();
    volatile bool ready, disposed;
    string stage = "Preparing your desktop";
    static int active;
    internal int WindowsCreated { get; private set; }
    internal string? Error { get; private set; }
    internal int RenderedFrames { get; private set; }
    internal double RenderedFPS { get; private set; }
    internal object? FramePacing { get; private set; }
    internal int TargetFPS => config.AnimationFramesPerSecond;
    internal bool Wait(int milliseconds) => thread.Join(milliseconds);
    internal static bool Active => Volatile.Read(ref active) > 0;
    internal static bool ShouldShow(Settings settings) => settings.StartupIntroEnabled && settings.ReplaceDesktop && (settings.FeatureVersion < 1 ? settings.ModsInitialized : settings.EnabledMods.Contains(ShellFeatures.Id("desktop-icons"), StringComparer.OrdinalIgnoreCase));
    internal StartupPresentation(Settings config, bool preview = false, bool probe = false)
    {
        this.config = config; ready = preview; this.probe = probe;
        thread = new Thread(Run) { IsBackground = true, Name = "Harbor startup cinema" }; thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    internal void Stage(string value) { stage = value; var d = dispatcher; if (d != null && !d.HasShutdownStarted) d.BeginInvoke(new Action(() => { foreach (var window in windows) if (window.Content is Grid grid && grid.Children.OfType<StartupArtwork>().FirstOrDefault() is { } art) art.Stage(value); })); }
    internal void Ready() { ready = true; Stage("Your desktop is ready"); }
    void Run()
    {
        Interlocked.Increment(ref active);
        try
        {
            dispatcher = Dispatcher.CurrentDispatcher; using var timing = new AnimationTiming(); var elapsed = Stopwatch.StartNew(); bool finishing = false;
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2); double scale = Native.Scale(monitor);
                var window = new Window { Title = "Harbor — starting up", Style = new Style(typeof(Window)), Width = screen.Bounds.Width / scale, Height = screen.Bounds.Height / scale, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Topmost = true, Background = HarborBackdrop.Brush(screen.Bounds.Width, screen.Bounds.Height) };
                var root = new Grid(); var art = new StartupArtwork(config.DesktopLogoSize); art.Stage(stage); root.Children.Add(art);
                if (!screen.Primary) { art.Opacity = .6; art.Progress = 1; }
                else
                {
                    var skip = new Button { Style = new Style(typeof(Button)), Content = "Skip intro", FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Foreground = new SolidColorBrush(Theme.CurrentColors.Muted), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24), Padding = new Thickness(12), Cursor = Cursors.Hand };
                    skip.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate>"); skip.Click += (_, _) => Finish(); root.Children.Add(skip);
                    // Start after both display windows have finished their first
                    // layout; allocating the second backdrop must not stall spin.
                }
                window.Content = root; window.SourceInitialized += (_, _) => { if (probe) return; Native.ToolWindow(window); Native.SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, 0x50); };
                window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Finish(); }; windows.Add(window);
                if (probe) { new WindowInteropHelper(window).EnsureHandle(); root.Measure(new Size(1280, 720)); root.Arrange(new Rect(0, 0, 1280, 720)); root.UpdateLayout(); var image = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 720, 96, 96, PixelFormats.Pbgra32); image.Render(root); } else window.Show();
                WindowsCreated++;
            }
            if (probe) { foreach (var window in windows) window.Close(); windows.Clear(); return; }
            dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => {
                if (finishing || disposed) return; elapsed.Restart();
                foreach (var window in windows) if (window.Content is Grid grid && grid.Children.OfType<Button>().Any()) foreach (var art in grid.Children.OfType<StartupArtwork>()) {
                    if (config.DockMotionMilliseconds == 0) art.Progress = 1;
                    else { var intro = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(2600)) { FillBehavior = FillBehavior.HoldEnd }; Timeline.SetDesiredFrameRate(intro, config.AnimationFramesPerSecond); art.BeginAnimation(StartupArtwork.ProgressProperty, intro); }
                }
            }));
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) }; timeout.Tick += (_, _) => { if (disposed || elapsed.ElapsedMilliseconds >= 8000 || ready && elapsed.ElapsedMilliseconds >= (config.DockMotionMilliseconds == 0 ? 350 : 2800)) Finish(); }; timeout.Start();
            if (disposed) Finish(); Dispatcher.Run(); timeout.Stop();
            void Finish()
            {
                if (finishing) return; finishing = true;
                if (disposed || config.DockMotionMilliseconds == 0) { CloseAll(); return; }
                foreach (var window in windows) { window.IsHitTestVisible = false; if (window.Content is not Grid grid) continue; foreach (var button in grid.Children.OfType<Button>()) button.Visibility = Visibility.Hidden; foreach (var art in grid.Children.OfType<StartupArtwork>()) art.FadeOut(config.AnimationFramesPerSecond, ReferenceEquals(window, windows.Last()) ? CloseAll : () => { }); }
            }
            void CloseAll() { foreach (var window in windows.ToArray()) { if (window.Content is Grid grid && grid.Children.OfType<StartupArtwork>().FirstOrDefault() is { } art && art.RenderedFrames > RenderedFrames) { RenderedFrames = art.RenderedFrames; RenderedFPS = art.RenderedFPS; FramePacing = art.FramePacing; } window.Close(); } windows.Clear(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        }
        catch (Exception ex) { Error = ex.ToString(); Log.Write("Startup intro: " + ex); try { foreach (var window in windows.ToArray()) window.Close(); } catch { } }
        finally { Interlocked.Decrement(ref active); }
    }
    public void Dispose()
    {
        disposed = true; var d = dispatcher;
        if (d != null && !d.HasShutdownStarted) d.BeginInvoke(new Action(() => { foreach (var window in windows.ToArray()) window.Close(); windows.Clear(); d.BeginInvokeShutdown(DispatcherPriority.Background); }));
    }
}
