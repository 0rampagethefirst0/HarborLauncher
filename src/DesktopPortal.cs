using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Harbor;

internal sealed class DesktopPortal : Grid
{
    readonly Shell shell;
    readonly IntPtr monitor;
    readonly HarborEmblem emblem;
    readonly Button button;
    bool held;
    internal HarborEmblem Emblem => emblem;
    internal object AnimationStatus => new { emblem.IsSpinning, TargetFPS = shell.Config.AnimationFramesPerSecond, emblem.FramePacing };
    internal DesktopPortal(Shell shell, IntPtr monitor)
    {
        this.shell = shell; this.monitor = monitor; HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        emblem = new HarborEmblem(shell.Config.DesktopLogoSize) { SampleFrames = true }; body.Children.Add(emblem);
        var name = Theme.Label("H A R B O R", 13, Theme.Accent); name.HorizontalAlignment = HorizontalAlignment.Center; name.Margin = new Thickness(0, 7, 0, 0); body.Children.Add(name);
        var caption = Theme.Label("Open your desktop", 12, Theme.Muted); caption.HorizontalAlignment = HorizontalAlignment.Center; caption.Margin = new Thickness(0, 8, 0, 0); body.Children.Add(caption);
        button = new Button { Content = body, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(16), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Open Harbor Home · your desktop apps and folders" };
        button.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate>");
        AutomationProperties.SetName(button, "Open Harbor Home"); button.Click += (_, _) => shell.OpenHome(monitor);
        Motion.Hover(button); Children.Add(button); Unloaded += (_, _) => emblem.Spin(false);
        Loaded += (_, _) => { var center = emblem.TranslatePoint(new Point(emblem.ActualWidth / 2, emblem.ActualHeight / 2), this); RenderTransform = new TranslateTransform(0, ActualHeight / 2 - center.Y); DragonFrameCache.Warm(shell, monitor); };
    }
    internal EmblemState? Hold()
    {
        if (PresentationSource.FromVisual(emblem) == null) return null;
        held = true; emblem.Spin(false); Motion.PauseHover(button, true);
        var top = emblem.PointToScreen(new Point(0, 0)); var bottom = emblem.PointToScreen(new Point(emblem.ActualWidth, emblem.ActualHeight));
        var state = new EmblemState(new Rect(top, bottom), emblem.Turn);
        // Keep the frozen desktop pose underneath Home until its own first frame
        // is ready. Removing it here can expose an empty wallpaper for one frame.
        return state;
    }
    internal void Release()
    {
        held = false; Visibility = Visibility.Visible; Motion.PauseHover(button, false);
    }
    internal void Tick(bool libraryOpen)
    {
        Visibility = libraryOpen && !held ? Visibility.Hidden : Visibility.Visible;
        bool exposed = !libraryOpen && !held && !shell.HideBars(monitor) && shell.Config.DesktopLogoSpin && shell.Config.DockMotionMilliseconds > 0;
        if (exposed && PresentationSource.FromVisual(this) != null)
        {
            var center = emblem.PointToScreen(new Point(emblem.ActualWidth / 2, emblem.ActualHeight / 2));
            exposed = !shell.Tracker.Windows.Where(w => w.Monitor == monitor && !Native.IsIconic(w.Handle)).Any(w => { Native.GetWindowRect(w.Handle, out var bounds); return center.X >= bounds.Left && center.X <= bounds.Right && center.Y >= bounds.Top && center.Y <= bounds.Bottom; });
        }
        emblem.Spin(exposed && Settings.TestDataDir == null, shell.Config.AnimationFramesPerSecond);
    }
}
