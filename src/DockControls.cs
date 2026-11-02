using System;
using System.Windows;
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
    readonly TextBlock status = Theme.Label("Changes save automatically", 11, Theme.Muted);
    readonly StackPanel body = new(), tabs = new() { Orientation = Orientation.Horizontal };
    readonly ScrollViewer scroll;
    string page = "Style";
    bool dirty;
    internal DockControls(Shell shell, IntPtr monitor, Native.Rect dockBounds)
    {
        this.shell = shell; Title = "Harbor — taskbar controls"; Theme.PopupWindow(this, 420, (int)Math.Min(630, Native.Monitor(monitor).Work.Height / Native.Scale(monitor) - 24), monitor, true);
        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) }; var close = Theme.Button(UiIcons.Icon("close"), "Close taskbar controls", Close); close.Padding = new Thickness(6); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close); header.Children.Add(Theme.Label("Taskbar", 23)); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        tabs.Margin = new Thickness(0, 0, 0, 12); DockPanel.SetDock(tabs, System.Windows.Controls.Dock.Top); root.Children.Add(tabs);
        status.Margin = new Thickness(2, 10, 0, 0); DockPanel.SetDock(status, System.Windows.Controls.Dock.Bottom); root.Children.Add(status);
        scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Motion.SmoothScroll(scroll); root.Children.Add(scroll);
        var card = Theme.Card(root, 17); var offset = new TranslateTransform(); card.RenderTransform = offset; Content = card;
        SourceInitialized += (_, _) =>
        {
            double scale = Native.Scale(monitor); var work = Native.Monitor(monitor).Monitor; int width = (int)(Width * scale), height = (int)(Height * scale);
            int x = Math.Clamp((dockBounds.Left + dockBounds.Right - width) / 2, work.Left + 10, Math.Max(work.Left + 10, work.Right - width - 10));
            int y = Math.Max(work.Top + shell.Config.TopBarHeight + 8, dockBounds.Top - height - 10);
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), x, y, width, height, 0x10);
        };
        Loaded += (_, _) => { int ms = shell.Config.DockMotionMilliseconds; if (ms > 0 && Settings.TestDataDir == null) offset.BeginAnimation(TranslateTransform.YProperty, Motion.Track(new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } })); };
        save.Tick += (_, _) => Commit(); Closed += (_, _) => { save.Stop(); Commit(); }; Theme.CloseOnDeactivate(this); Render(false);
    }
    void Render(bool keep = true)
    {
        using var viewport = ScrollPosition.Keep(scroll, keep);
        BarSettingsUi.Tabs(tabs, new[] { "Style", "Interaction", "Apps" }, page, next => { page = next; Render(false); }); body.Children.Clear(); var c = shell.Config;
        if (page == "Style")
        {
            var size = BarSettingsUi.Section(body, "Size"); Slider(size, "App icons", 24, 44, c.IconSize, "px", v => c.IconSize = (int)v);
            var transparency = BarSettingsUi.Section(body, "Transparency"); Slider(transparency, "At rest", .5, 99.5, c.TaskbarBaseTransparency, "%", v => c.TaskbarBaseTransparency = v, .5); Slider(transparency, "On hover", .5, 99.5, c.TaskbarHoverTransparency, "%", v => { c.TaskbarHoverTransparency = v; c.DockFadeOpacity = 1 - v / 100; }, .5); Slider(transparency, "After right-click", .5, 99.5, c.TaskbarActiveTransparency, "%", v => c.TaskbarActiveTransparency = v, .5);
            BarSettingsUi.Advanced(body, "Shape and animation", advanced => { Slider(advanced, "Corner radius", 0, 36, c.DockRadius, "px", v => c.DockRadius = (int)v); Slider(advanced, "Animation time", 0, 350, c.DockMotionMilliseconds, "ms", v => c.DockMotionMilliseconds = (int)v, 10); });
        }
        else if (page == "Interaction")
        {
            var interaction = BarSettingsUi.Section(body, "Mouse and keyboard"); Toggle(interaction, "Click through on hover", c.DockHoverThrough, v => c.DockHoverThrough = v); Toggle(interaction, "Stay faded while typing", c.DockQuietWhileTyping, v => c.DockQuietWhileTyping = v); Slider(interaction, "Stay active after leaving", .5, 10, c.DockInteractionDelay / 1000d, "s", v => c.DockInteractionDelay = (int)Math.Round(v * 1000), .5);
            BarSettingsUi.Note(body, "Right-click once to activate the taskbar. Right-click again for app menus.");
        }
        else
        {
            var apps = BarSettingsUi.Section(body, "Apps"); Toggle(apps, "Show open apps", c.TaskbarShowRunningApps, v => c.TaskbarShowRunningApps = v); BarSettingsUi.Note(apps, "Pinned apps and folders stay visible.");
            var folders = BarSettingsUi.Section(body, "Bulky folders"); Slider(folders, "Apps inside folders", 24, 96, c.BulkAppSize, "px", v => { c.BulkAppSize = (int)v; shell.SaveShortcuts(); }); Slider(folders, "Folder icon size", 40, 72, c.TaskbarFolderSize, "px", v => c.TaskbarFolderSize = (int)v);
        }
        if (!keep) Motion.Enter(body, 0);
    }
    void Toggle(Panel host, string title, bool initial, Action<bool> set) => BarSettingsUi.Toggle(host, title, initial, v => { set(v); Changed(); });
    void Slider(Panel host, string title, double min, double max, double initial, string unit, Action<double> set, double step = 1) => BarSettingsUi.Slider(host, title, min, max, initial, unit, v => { set(v); Changed(); }, step);
    void Changed() { dirty = true; status.Text = "Saving…"; shell.ApplyDockPreferences(); save.Stop(); save.Start(); }
    void Commit() { save.Stop(); if (!dirty) return; try { shell.Config.Save(); dirty = false; status.Text = "Saved"; } catch (Exception ex) { status.Text = "Could not save"; Log.Write("Taskbar controls: " + ex.Message); } }
    internal void PreparePreview(string name) { page = name; Render(false); }
}
