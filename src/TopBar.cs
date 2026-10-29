using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Harbor;

internal sealed class TopBar : Window
{
    const string DragFormat = "Harbor.TopDockItem";
    readonly Shell shell;
    internal readonly IntPtr MonitorHandle;
    readonly Grid layout = new();
    readonly Dictionary<string, StackPanel> groups = new();
    readonly Dictionary<string, FrameworkElement> elements = new();
    readonly TextBlock title = Theme.Label("Desktop", 12, Theme.Muted), clock = Theme.Label("", 12), stats = Theme.Label("", 11, Theme.Muted);
    readonly Border frame;
    AppBar? appbar;
    IntPtr lastExternal;
    string spec = "";
    bool dragging;
    long suppressClick;
    internal TopBar(Shell shell, IntPtr monitor)
    {
        this.shell = shell; MonitorHandle = monitor; Title = "Harbor — top Dock"; Width = 1000; Height = shell.Config.TopBarHeight;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent;
        layout.Margin = new Thickness(10, 0, 10, 6); layout.ColumnDefinitions.Add(new ColumnDefinition()); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); layout.ColumnDefinitions.Add(new ColumnDefinition());
        int column = 0; foreach (string group in new[] { "left", "center", "right" })
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = group == "right" ? HorizontalAlignment.Right : HorizontalAlignment.Left, AllowDrop = true };
            var scroll = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false }; Grid.SetColumn(scroll, column++); layout.Children.Add(scroll); groups[group] = panel;
            panel.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            panel.Drop += (_, e) => { if (e.Data.GetData(DragFormat) is string id) { MoveButton(id, null, group); e.Handled = true; } };
        }
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var root = new Grid(); frame = new Border { Background = Theme.Bg, Child = layout }; root.Children.Add(frame);
        var arrow = Theme.Button(Theme.Glyph("\uE70D", 9), "Dock controls", () => shell.OpenTopDockControls(monitor)); arrow.Width = 30; arrow.Height = 11; arrow.MinHeight = 0; arrow.Padding = new Thickness(0); arrow.HorizontalAlignment = HorizontalAlignment.Center; arrow.VerticalAlignment = VerticalAlignment.Bottom; arrow.Background = Theme.Raised; root.Children.Add(arrow); Content = root;
        ContextMenu = BackgroundMenu(); RefreshLayout();
        SourceInitialized += (_, _) => { Native.ToolWindow(this); appbar = new AppBar(this, monitor, 1, shell.Config.TopBarHeight, overlay: shell.Config.ReplaceTaskbar || !shell.Config.TopDockReserveSpace); Tick(); };
        Closed += (_, _) => appbar?.Dispose();
        PreviewMouseWheel += (_, e) => { if (dragging || shell.Tweaks.Module("toolbar-wheel-volume") is not { } mod || shell.Mods.Value(mod, "modifier", "no") == "yes" && (System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Alt) == 0) return; Audio.SetVolume(Audio.Read().Volume + Math.Sign(e.Delta) * (float)shell.Mods.Number(mod, "step", 2, 1, 20) / 100); e.Handled = true; };
    }
    ContextMenu BackgroundMenu()
    {
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Dock controls", () => shell.OpenTopDockControls(MonitorHandle)));
        menu.Items.Add(Theme.Item("Add divider", () => { BarLayout.AddDivider(shell.Config, "right"); Changed(); }));
        menu.Items.Add(Theme.Item("Add space", () => { BarLayout.AddDivider(shell.Config, "right", space: true); Changed(); }));
        menu.Items.Add(Theme.Item("Arrange windows", () => shell.OpenArrange(MonitorHandle))); menu.Items.Add(Theme.Item("Apps", () => shell.ToggleLauncher(MonitorHandle))); menu.Items.Add(Theme.Item("Mods", shell.OpenMods)); menu.Items.Add(Theme.Item("Harbor settings", shell.OpenSettings));
        menu.Items.Add(new Separator()); menu.Items.Add(Theme.Item("Exit Harbor and restore Windows", shell.Exit)); return menu;
    }
    FrameworkElement Make(BarItem item)
    {
        FrameworkElement element;
        if (item.Id.StartsWith("divider.")) element = new Border { Width = item.Space ? 20 : 1, Height = 18, VerticalAlignment = VerticalAlignment.Center, Background = item.Space ? Brushes.Transparent : Theme.Line };
        else
        {
            UIElement content = item.Id switch {
                "apps" => shell.Config.TopDockBrandText ? UiIcons.Label("Harbor", shell.Config.TopDockFontSize, "apps") : Theme.Logo(shell.Config.TopDockIconSize),
                "title" => title, "clock" => clock, "stats" => stats,
                _ => UiIcons.Icon(item.Id == "settings" ? "controls" : item.Id, shell.Config.TopDockIconSize)
            };
            var button = Theme.Button(content, BarLayout.Label(item), () => { if (Environment.TickCount64 < suppressClick) return; ActivateItem(item.Id); }); button.Padding = new Thickness(shell.Config.TopDockPadding, 0, shell.Config.TopDockPadding, 0); element = button;
            if (item.Id == "title") button.MouseDoubleClick += (_, e) => { if (shell.Tweaks.Has("title-actions") && Target() != IntPtr.Zero) { Native.ShowWindow(lastExternal, Native.IsZoomed(lastExternal) ? 9 : 3); Native.Activate(lastExternal); e.Handled = true; } };
        }
        element.Tag = item.Id; element.Margin = new Thickness(shell.Config.TopDockGap + (item.Id.StartsWith("divider.") ? 7 : 0), 0, shell.Config.TopDockGap + (item.Id.StartsWith("divider.") ? 7 : 0), 0);
        AutomationProperties.SetAutomationId(element, "top-dock:" + item.Id); element.ToolTip = BarLayout.Label(item) + " · drag to move · right-click to customize";
        var menu = new ContextMenu(); element.ContextMenu = menu; element.ContextMenuOpening += (_, _) => ItemMenu(menu, item);
        Point origin = default;
        element.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(this);
        element.PreviewMouseMove += (_, e) => {
            if (dragging || e.LeftButton != MouseButtonState.Pressed) return; Point p = e.GetPosition(this);
            if (Math.Abs(p.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragging = true; try { DragDrop.DoDragDrop(element, new DataObject(DragFormat, item.Id), DragDropEffects.Move); } finally { dragging = false; suppressClick = Environment.TickCount64 + 300; Dispatcher.BeginInvoke(new Action(RefreshLayout)); }
        };
        element.AllowDrop = true; element.DragOver += (_, e) => { if (!e.Data.GetDataPresent(DragFormat)) return; e.Effects = DragDropEffects.Move; e.Handled = true; };
        element.Drop += (_, e) => { if (e.Data.GetData(DragFormat) is string id) { MoveButton(id, item.Id, item.Group); e.Handled = true; } };
        return element;
    }
    void ActivateItem(string id)
    {
        switch (id) {
            case "apps": shell.ToggleLauncher(MonitorHandle); break; case "clock": shell.OpenCalendar(MonitorHandle); break; case "stats": Native.Launch("taskmgr.exe"); break;
            case "previous": Native.Media(0xB1); break; case "play": Native.Media(0xB3); break; case "next": Native.Media(0xB0); break;
            case "network": case "volume": shell.OpenQuick(MonitorHandle); break; case "files": shell.OpenFiles(); break; case "mods": shell.OpenMods(); break;
            case "notifications": shell.OpenNotifications(MonitorHandle); break; case "settings": shell.OpenSettings(); break;
        }
    }
    void ItemMenu(ContextMenu menu, BarItem item)
    {
        menu.Items.Clear(); menu.Items.Add(Theme.Item("Dock controls", () => shell.OpenTopDockControls(MonitorHandle)));
        foreach (string group in new[] { "left", "center", "right" }) menu.Items.Add(Theme.Item("Move to " + group, () => { BarLayout.Move(shell.Config, item.Id, null, group); Changed(); }));
        menu.Items.Add(Theme.Item("Add divider before", () => { BarLayout.AddDivider(shell.Config, item.Group, item.Id); Changed(); })); menu.Items.Add(Theme.Item("Add space before", () => { BarLayout.AddDivider(shell.Config, item.Group, item.Id, true); Changed(); }));
        menu.Items.Add(Theme.Item(item.Id.StartsWith("divider.") ? "Remove divider" : "Hide button", () => { if (item.Id.StartsWith("divider.")) shell.Config.TopDockItems.Remove(item); else BarLayout.Enable(shell.Config, item, false); Changed(); }));
        if (item.Id == "title" && shell.Tweaks.Has("title-actions") && Target() != IntPtr.Zero)
        {
            var h = lastExternal; menu.Items.Add(new Separator()); menu.Items.Add(Theme.Item("Minimize window", () => Native.ShowWindow(h, 6))); menu.Items.Add(Theme.Item(Native.IsZoomed(h) ? "Restore window" : "Maximize window", () => { Native.ShowWindow(h, Native.IsZoomed(h) ? 9 : 3); Native.Activate(h); }));
            menu.Items.Add(Theme.Item("Always on top", () => { try { var saved = shell.Recovery?.AcquireEffect(h, "window-topmost"); if (saved != null) WindowEffects.SetTopmost(saved, (Native.GetWindowLong(h, -20).ToInt64() & 8) == 0); } catch (Exception ex) { shell.Notifications.Add("Window could not be changed", ex.Message); } }));
            var entry = shell.Tracker.Windows.FirstOrDefault(w => w.Handle == h); if (entry != null) menu.Items.Add(Theme.Item("Mods for this app", () => shell.OpenAppMods(entry))); menu.Items.Add(Theme.Item("Close window", () => Native.PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero)));
        }
    }
    void Changed() { shell.Config.Save(); shell.ApplyTopDockPreferences(); }
    internal void MoveButton(string id, string? before, string group) { BarLayout.Move(shell.Config, id, before, group); Changed(); }
    internal void RefreshLayout()
    {
        if (dragging || IsMouseCaptureWithin || IsMouseOver && Mouse.LeftButton == MouseButtonState.Pressed || ContextMenu?.IsOpen == true || elements.Values.Any(e => e.ContextMenu?.IsOpen == true)) return;
        var c = shell.Config; string next = string.Join("|", c.TopDockItems.Select(i => i.Id + i.Group + i.Enabled + i.Space + BarLayout.Legacy(c, i.Id))) + $"/{c.TopDockFontSize}/{c.TopDockIconSize}/{c.TopDockPadding}/{c.TopDockGap}/{c.TopDockBrandText}";
        if (next != spec) {
            foreach (var old in elements.Values.OfType<ContentControl>()) old.Content = null;
            foreach (var panel in groups.Values) panel.Children.Clear(); elements.Clear();
            foreach (var item in c.TopDockItems.Where(i => i.Enabled && BarLayout.Legacy(c, i.Id))) { var element = Make(item); elements[item.Id] = element; groups[item.Group].Children.Add(element); } spec = next;
        }
        title.FontSize = clock.FontSize = c.TopDockFontSize; stats.FontSize = Math.Max(10, c.TopDockFontSize - 1); title.MaxWidth = c.TopDockTitleWidth;
        Height = c.TopBarHeight; Topmost = c.TopDockOnTop; Opacity = c.TopDockOpacity; frame.CornerRadius = new CornerRadius(c.TopDockRadius); frame.BorderBrush = Theme.Line; frame.BorderThickness = c.TopDockShowBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0); appbar?.Configure(c.TopBarHeight);
    }
    internal void Tick()
    {
        RefreshLayout(); var c = shell.Config;
        string format = c.Use24HourClock ? c.TopDockSeconds ? "ddd, MMM d   HH:mm:ss" : "ddd, MMM d   HH:mm" : c.TopDockSeconds ? "ddd, MMM d   h:mm:ss tt" : "ddd, MMM d   h:mm tt";
        if (shell.Tweaks.Module("clock-seconds") is { } custom) format = shell.Mods.Value(custom, "format", format);
        try { clock.Text = DateTime.Now.ToString(format); } catch (FormatException) { clock.Text = DateTime.Now.ToString("HH:mm:ss"); }
        stats.Text = $"CPU {shell.CpuUsage:0}%  RAM {shell.MemoryUsage}%";
        var h = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(h, out uint pid);
        if (pid != Environment.ProcessId) { title.Text = string.IsNullOrWhiteSpace(Native.Title(h)) ? "Desktop" : Native.Title(h); if (shell.Tracker.Windows.Any(w => w.Handle == h)) lastExternal = h; }
        SetFullscreen(shell.HideBars(MonitorHandle));
    }
    IntPtr Target() { Native.GetWindowThreadProcessId(lastExternal, out uint pid); return Native.IsWindow(lastExternal) && !shell.Games.IsGame(Native.ProcessPath(pid)) ? lastExternal : IntPtr.Zero; }
    internal void PreparePreview() { RefreshLayout(); clock.Text = DateTime.Now.ToString(shell.Config.Use24HourClock ? "ddd, MMM d   HH:mm" : "ddd, MMM d   h:mm tt"); stats.Text = "CPU 24%  RAM 42%"; title.Text = "Projects — Harbor Files"; }
    internal void SetFullscreen(bool hide)
    {
        bool primary = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Contains(Native.Monitor(MonitorHandle).Monitor.Left + 1, Native.Monitor(MonitorHandle).Monitor.Top + 1);
        hide |= !shell.Config.TopDockEnabled || shell.Config.TopDockPrimaryOnly && !primary;
        appbar?.SetOverlay(shell.Config.ReplaceTaskbar || !shell.Config.TopDockReserveSpace); appbar?.SetSuspended(hide);
        if (hide && IsVisible) Hide(); else if (!hide && !IsVisible) { Show(); appbar?.Position(); }
    }
}
