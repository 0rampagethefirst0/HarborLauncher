using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Harbor;

internal sealed class DockControls : Window
{
    readonly Shell shell;
    readonly DispatcherTimer save = new() { Interval = TimeSpan.FromMilliseconds(220) };
    readonly TextBlock status = Theme.Label("", 11, Theme.Muted);
    bool dirty;
    internal DockControls(Shell shell, IntPtr monitor, Native.Rect dockBounds)
    {
        this.shell = shell; Title = "Harbor — taskbar controls"; Theme.PopupWindow(this, 370, 430, monitor, true);
        var body = new StackPanel { Margin = new Thickness(20, 17, 20, 17) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) }; var close = Theme.IconButton("\uE8BB", "Close dock controls", Close); close.Padding = new Thickness(5); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close); header.Children.Add(new TextBlock { Text = "Taskbar controls", FontSize = 21, FontWeight = FontWeights.SemiBold }); body.Children.Add(header);
        Toggle("Click-through on hover", shell.Config.DockHoverThrough, value => shell.Config.DockHoverThrough = value);
        Toggle("Stay faded while typing", shell.Config.DockQuietWhileTyping, value => shell.Config.DockQuietWhileTyping = value);
        Slider("Faded opacity", 20, 80, shell.Config.DockFadeOpacity * 100, "%", value => shell.Config.DockFadeOpacity = value / 100);
        Slider("Active after leaving", 1, 10, shell.Config.DockInteractionDelay / 1000.0, "s", value => shell.Config.DockInteractionDelay = (int)value * 1000);
        Slider("Icon size", 24, 44, shell.Config.IconSize, "px", value => shell.Config.IconSize = (int)value);
        Slider("Motion duration", 0, 350, shell.Config.DockMotionMilliseconds, "ms", value => shell.Config.DockMotionMilliseconds = (int)value);
        body.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 7, 0, 9) });
        var footer = new DockPanel(); DockPanel.SetDock(status, System.Windows.Controls.Dock.Right); footer.Children.Add(status); footer.Children.Add(Theme.Label("Right-click once to activate; again for menus.", 10, Theme.Muted)); body.Children.Add(footer);
        var card = Theme.Card(body, 17); var offset = new TranslateTransform(); card.RenderTransform = offset; Content = card;
        SourceInitialized += (_, _) =>
        {
            double scale = Native.Scale(monitor); var work = Native.Monitor(monitor).Monitor;
            int width = (int)(Width * scale), height = (int)(Height * scale);
            int x = Math.Clamp((dockBounds.Left + dockBounds.Right - width) / 2, work.Left + 10, Math.Max(work.Left + 10, work.Right - width - 10));
            int y = Math.Max(work.Top + shell.Config.TopBarHeight + 8, dockBounds.Top - height - 10);
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), x, y, width, height, 0x50);
        };
        Loaded += (_, _) =>
        {
            int ms = shell.Config.DockMotionMilliseconds;
            if (ms <= 0 || Settings.TestDataDir != null) return;
            var duration = TimeSpan.FromMilliseconds(ms);
            offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(22, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
            card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.Stop });
        };
        save.Tick += (_, _) => Commit(); Closed += (_, _) => { save.Stop(); Commit(); }; Theme.CloseOnDeactivate(this);
        void Toggle(string title, bool initial, Action<bool> set)
        {
            var box = new CheckBox { Content = UiIcons.Label(title, 13), Style = (Style)FindResource("Harbor.Switch"), IsChecked = initial, FontSize = 13, Margin = new Thickness(0, 4, 0, 8) }; AutomationProperties.SetName(box, title);
            box.Checked += (_, _) => { set(true); Changed(); }; box.Unchecked += (_, _) => { set(false); Changed(); }; body.Children.Add(box);
        }
        void Slider(string title, double min, double max, double initial, string unit, Action<double> set)
        {
            var row = new DockPanel { Margin = new Thickness(0, 9, 0, 0) }; var value = Theme.Label(initial.ToString("0") + " " + unit, 11, Theme.Muted); DockPanel.SetDock(value, System.Windows.Controls.Dock.Right); row.Children.Add(value); row.Children.Add(UiIcons.Label(title, 12)); body.Children.Add(row);
            var slider = new Slider { Minimum = min, Maximum = max, Value = initial, TickFrequency = unit == "ms" ? 10 : 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 3, 0, 0) }; AutomationProperties.SetName(slider, title);
            slider.ValueChanged += (_, _) => { set(slider.Value); value.Text = slider.Value.ToString("0") + " " + unit; Changed(); }; body.Children.Add(slider);
        }
    }
    void Changed()
    {
        dirty = true; status.Text = "Saving…"; shell.ApplyDockPreferences(); save.Stop(); save.Start();
    }
    void Commit()
    {
        save.Stop(); if (!dirty) return;
        try { shell.Config.Save(); dirty = false; status.Text = "Saved"; } catch (Exception ex) { status.Text = "Could not save"; Log.Write("Dock controls: " + ex.Message); }
    }
}
