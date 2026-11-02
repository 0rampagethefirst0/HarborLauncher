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
    readonly Shell shell;
    internal readonly IntPtr MonitorHandle;
    readonly DockPlacementPanel layout;
    readonly Dictionary<string, FrameworkElement> elements = new();
    readonly Dictionary<string, TextBlock> labels = new();
    bool preview;
    string activeTitle = "Desktop";
    readonly Border frame;
    readonly Grid root = new() { Background = Brushes.Transparent, AllowDrop = true };
    readonly Border dropOutline = new() { BorderBrush = Theme.Accent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(5), IsHitTestVisible = false, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    AppBar? appbar;
    IntPtr lastExternal;
    string spec = "";
    bool dragging;
    long suppressClick;
    internal TopBar(Shell shell, IntPtr monitor)
    {
        this.shell = shell; MonitorHandle = monitor; Title = "Harbor — top Dock"; Width = 1000; Height = shell.Config.TopBarHeight;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent;
        layout = new DockPlacementPanel(shell.Config);
        var scroll = new ScrollViewer { Content = layout, Margin = new Thickness(10, 0, 10, 6), HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, CanContentScroll = false };
        frame = new Border { Background = Theme.Bg, Child = scroll }; root.Children.Add(frame);
        var arrow = Theme.Button(Theme.Glyph("\uE70D", 9), "Dock controls", () => shell.OpenTopDockControls(monitor)); arrow.Width = 30; arrow.Height = 11; arrow.MinHeight = 0; arrow.Padding = new Thickness(0); arrow.HorizontalAlignment = HorizontalAlignment.Center; arrow.VerticalAlignment = VerticalAlignment.Bottom; arrow.Background = Theme.Raised; root.Children.Add(arrow); Content = root;
        root.Children.Add(dropOutline);
        root.PreviewDragOver += (_, e) => {
            if (e.Data.GetData(BarLayout.DragFormat) is not string id || !elements.ContainsKey(id)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
            var point = e.GetPosition(layout); PreviewPlacement(id, point.X); e.Effects = DragDropEffects.Move; e.Handled = true;
            var cursor = e.GetPosition(scroll); if (cursor.X < 28) scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - 18); else if (cursor.X > scroll.ActualWidth - 28) scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + 18);
        };
        root.PreviewDrop += (_, e) => { if (e.Data.GetData(BarLayout.DragFormat) is string id) { PlaceButton(id, e.GetPosition(layout).X); e.Handled = true; } };
        root.DragLeave += (_, e) => { var p = e.GetPosition(root); if (!new Rect(root.RenderSize).Contains(p)) ClearPlacementPreview(); };
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
        string kind = BarLayout.Component(item);
        if (item.Id.StartsWith("divider.")) { var divider = new DividerVisual(value => item.Width = (int)value, Changed); divider.Configure(item.Width, item.DividerThickness, item.DividerLength, item.Space, shell.Config.TopBarHeight - 8); element = divider; }
        else
        {
            UIElement content = kind switch {
                "apps" => shell.Config.TopDockBrandText ? UiIcons.Label("Harbor", shell.Config.TopDockFontSize, "apps") : Theme.Logo(shell.Config.TopDockIconSize),
                "title" or "clock" or "date" or "time" or "cpu" or "memory" or "stats" or "text" => Text(),
                "app" => Theme.AppIcon(item.Target, BarLayout.Label(item), shell.Config.TopDockIconSize),
                "plugin" => PluginContent(),
                _ => UiIcons.Icon(kind == "settings" ? "controls" : kind, shell.Config.TopDockIconSize)
            };
            var button = Theme.Button(content, BarLayout.Label(item), () => { if (Environment.TickCount64 < suppressClick) return; if (kind == "app") { if (item.Target.Length > 0) shell.Launch(item.Target); } else if (kind == "plugin") shell.OpenPlugin(item.Target, MonitorHandle); else ActivateItem(kind); }); button.Padding = new Thickness(shell.Config.TopDockPadding, 0, shell.Config.TopDockPadding, 0); element = button;
            if (kind == "title") button.MouseDoubleClick += (_, e) => { if (shell.Tweaks.Has("title-actions") && Target() != IntPtr.Zero) { Native.ShowWindow(lastExternal, Native.IsZoomed(lastExternal) ? 9 : 3); Native.Activate(lastExternal); e.Handled = true; } };
        }
        if (item.Width > 0 && !item.Id.StartsWith("divider.")) element.Width = item.Width;
        element.Tag = item.Id; element.AllowDrop = true; element.Margin = new Thickness(shell.Config.TopDockGap + (item.Id.StartsWith("divider.") ? 7 : 0), 0, shell.Config.TopDockGap + (item.Id.StartsWith("divider.") ? 7 : 0), 0);
        AutomationProperties.SetAutomationId(element, "top-dock:" + item.Id); element.ToolTip = BarLayout.Label(item) + " · drag to move · right-click to customize";
        var menu = new ContextMenu(); element.ContextMenu = menu; element.ContextMenuOpening += (_, _) => ItemMenu(menu, item);
        Point origin = default; bool armed = false;
        element.PreviewMouseLeftButtonDown += (_, e) => { origin = e.GetPosition(root); armed = true; };
        element.PreviewMouseLeftButtonUp += (_, _) => armed = false;
        element.PreviewMouseMove += (_, e) => {
            if (!armed || dragging || e.LeftButton != MouseButtonState.Pressed) return; Point p = e.GetPosition(root);
            if (Math.Abs(p.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            armed = false; dragging = true; suppressClick = long.MaxValue; element.ReleaseMouseCapture();
            try { DragDrop.DoDragDrop(element, new DataObject(BarLayout.DragFormat, item.Id), DragDropEffects.Move); } finally { dragging = false; suppressClick = Environment.TickCount64 + 300; ClearPlacementPreview(); Dispatcher.BeginInvoke(new Action(RefreshLayout)); }
        };
        return element;
        TextBlock Text() { var text = Theme.Label("", item.FontSize > 0 ? item.FontSize : shell.Config.TopDockFontSize, kind is "title" or "stats" or "cpu" or "memory" ? Theme.Muted : null); text.TextTrimming = TextTrimming.CharacterEllipsis; if (kind == "title") text.MaxWidth = shell.Config.TopDockTitleWidth; labels[item.Id] = text; return text; }
        UIElement PluginContent() { var panel = new StackPanel { Orientation = Orientation.Horizontal }; var mod = shell.Mods.Catalog.FirstOrDefault(m => m.Id == item.Target); panel.Children.Add(UiIcons.Icon(mod == null ? "mods" : PluginPresentation.Icon(mod), shell.Config.TopDockIconSize)); var text = Text(); text.Margin = new Thickness(6, 0, 0, 0); text.MaxWidth = 130; panel.Children.Add(text); return panel; }
    }
    void ActivateItem(string id)
    {
        switch (id) {
            case "apps": shell.ToggleLauncher(MonitorHandle); break; case "clock": case "date": case "time": shell.OpenCalendar(MonitorHandle); break; case "stats": case "cpu": case "memory": Native.Launch("taskmgr.exe"); break;
            case "previous": Native.Media(0xB1); break; case "play": Native.Media(0xB3); break; case "next": Native.Media(0xB0); break;
            case "network": case "volume": shell.OpenQuick(MonitorHandle); break; case "files": shell.OpenFiles(); break; case "mods": shell.OpenMods(); break;
            case "notifications": shell.OpenNotifications(MonitorHandle); break; case "settings": shell.OpenSettings(); break;
        }
    }
    void ItemMenu(ContextMenu menu, BarItem item)
    {
        menu.Items.Clear(); menu.Items.Add(Theme.Item("Dock controls", () => shell.OpenTopDockControls(MonitorHandle)));
        if (item.Id.StartsWith("divider.")) menu.Items.Add(Theme.Item("Divider settings", () => new DividerEditor(shell, "Dock divider", () => item.Width <= 0 ? 24 : item.Width, v => item.Width = (int)v, () => item.DividerThickness, v => item.DividerThickness = v, () => item.DividerLength, v => item.DividerLength = v, () => item.Space, v => item.Space = v, direction => { var slot = layout.Slots.FirstOrDefault(s => s.Id == item.Id); if (slot.Id != null) PlaceButton(item.Id, slot.Left + slot.Width / 2 + direction * 12); }, Changed).Show()));
        menu.Items.Add(Theme.Item("Edit piece", () => new DockPieceEditor(shell, item).Show()));
        menu.Items.Add(Theme.Item("Duplicate", () => { BarLayout.Duplicate(shell.Config, item); Changed(); }));
        foreach (string group in new[] { "left", "center", "right" }) menu.Items.Add(Theme.Item("Move to " + group, () => { BarLayout.Move(shell.Config, item.Id, null, group); Changed(); }));
        menu.Items.Add(Theme.Item("Add divider before", () => { BarLayout.AddDivider(shell.Config, item.Group, item.Id); Changed(); })); menu.Items.Add(Theme.Item("Add space before", () => { BarLayout.AddDivider(shell.Config, item.Group, item.Id, true); Changed(); }));
        menu.Items.Add(Theme.Item(item.Id.StartsWith("divider.") ? "Remove divider" : "Hide button", () => { if (item.Id.StartsWith("divider.")) shell.Config.TopDockItems.Remove(item); else BarLayout.Enable(shell.Config, item, false); if (item.Id.StartsWith("part.plugin.")) shell.RebuildMods(); Changed(); }));
        if (item.Id.StartsWith("part.")) menu.Items.Add(Theme.Item("Remove piece", () => { if (item.Id.StartsWith("part.plugin.")) { BarLayout.Enable(shell.Config, item, false); shell.RebuildMods(); } shell.Config.TopDockItems.Remove(item); Changed(); }));
        if (BarLayout.Component(item) == "plugin" && shell.Mods.Catalog.FirstOrDefault(m => m.Id == item.Target) is { } plugin) { var access = shell.Plugins.Menu(plugin, MonitorHandle); menu.Items.Add(new Separator()); foreach (var action in access.Items.Cast<object>().ToArray()) { access.Items.Remove(action); menu.Items.Add(action); } }
        if (item.Id == "title" && shell.Tweaks.Has("title-actions") && Target() != IntPtr.Zero)
        {
            var h = lastExternal; menu.Items.Add(new Separator()); menu.Items.Add(Theme.Item("Minimize window", () => Native.ShowWindow(h, 6))); menu.Items.Add(Theme.Item(Native.IsZoomed(h) ? "Restore window" : "Maximize window", () => { Native.ShowWindow(h, Native.IsZoomed(h) ? 9 : 3); Native.Activate(h); }));
            menu.Items.Add(Theme.Item("Always on top", () => { try { var saved = shell.Recovery?.AcquireEffect(h, "window-topmost"); if (saved != null) WindowEffects.SetTopmost(saved, (Native.GetWindowLong(h, -20).ToInt64() & 8) == 0); } catch (Exception ex) { shell.Notifications.Add("Window could not be changed", ex.Message); } }));
            var entry = shell.Tracker.Windows.FirstOrDefault(w => w.Handle == h); if (entry != null) menu.Items.Add(Theme.Item("Mods for this app", () => shell.OpenAppMods(entry))); menu.Items.Add(Theme.Item("Close window", () => Native.PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero)));
        }
    }
    void Changed() { shell.Config.Save(); shell.ApplyTopDockPreferences(); }
    internal void MoveButton(string id, string? before, string group) { BarLayout.Move(shell.Config, id, before, group); Changed(); }
    internal bool PlaceButton(string id, double center) { bool placed = layout.Place(id, center); ClearPlacementPreview(); if (placed) Changed(); return placed; }
    internal void PreviewPlacement(string id, double center)
    {
        if (!double.IsFinite(center) || layout.UsableWidth <= 0 || !elements.ContainsKey(id)) return;
        layout.Preview(id, Math.Clamp(center / layout.UsableWidth, 0, 1));
        var slot = layout.Slots.First(s => s.Id == id); var origin = layout.TranslatePoint(new Point(slot.Left, 1), root);
        dropOutline.Width = slot.Width; dropOutline.Height = Math.Max(1, layout.ActualHeight - 2); dropOutline.Margin = new Thickness(Math.Max(0, origin.X), origin.Y, 0, 0); dropOutline.Visibility = Visibility.Visible;
    }
    internal void ClearPlacementPreview() { layout.Preview(null, null); dropOutline.Visibility = Visibility.Collapsed; }
    internal DockPlacementPanel PlacementPanel => layout;
    internal Grid PlacementSurface => root;
    internal void RefreshLayout()
    {
        if (dragging || IsMouseCaptureWithin || IsMouseOver && Mouse.LeftButton == MouseButtonState.Pressed || ContextMenu?.IsOpen == true || elements.Values.Any(e => e.ContextMenu?.IsOpen == true)) return;
        var c = shell.Config; string next = string.Join("|", c.TopDockItems.Select(i => i.Id + i.Group + i.Enabled + i.Space + i.Kind + i.Name + i.Text + i.Format + i.TimeZone + i.Target + i.Width + i.FontSize + i.DividerThickness + i.DividerLength + BarLayout.Legacy(c, BarLayout.Component(i)))) + $"/{c.TopDockFontSize}/{c.TopDockIconSize}/{c.TopDockPadding}/{c.TopDockGap}/{c.TopDockBrandText}/{c.TopDockTitleWidth}";
        next += "/" + string.Join(",", shell.Mods.Enabled.Where(m => m.Kind == "widget").Select(m => m.Id));
        if (next != spec) {
            foreach (var old in elements.Values.OfType<ContentControl>()) old.Content = null;
            layout.Children.Clear(); elements.Clear(); labels.Clear();
            foreach (var item in c.TopDockItems.Where(i => i.Enabled && (!i.Id.StartsWith("divider.") || ShellFeatures.On(c, "dock-dividers")) && BarLayout.Legacy(c, BarLayout.Component(i)) && (BarLayout.Component(i) != "plugin" || shell.Mods.Enabled.Any(m => m.Id == i.Target)))) { var element = Make(item); elements[item.Id] = element; layout.Children.Add(element); } spec = next;
        }
        layout.InvalidateArrange();
        UpdateLabels();
        Height = c.TopBarHeight; Topmost = c.TopDockOnTop; Opacity = c.TopDockOpacity; frame.CornerRadius = new CornerRadius(c.TopDockRadius); frame.BorderBrush = Theme.Line; frame.BorderThickness = c.TopDockShowBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0); appbar?.Configure(c.TopBarHeight);
    }
    internal void Tick()
    {
        RefreshLayout(); var c = shell.Config;
        var h = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(h, out uint pid);
        if (pid != Environment.ProcessId) { activeTitle = string.IsNullOrWhiteSpace(Native.Title(h)) ? "Desktop" : Native.Title(h); if (shell.Tracker.Windows.Any(w => w.Handle == h)) lastExternal = h; }
        UpdateLabels();
        SetFullscreen(shell.HideBars(MonitorHandle));
    }
    IntPtr Target() { Native.GetWindowThreadProcessId(lastExternal, out uint pid); return Native.IsWindow(lastExternal) && !shell.Games.IsGame(Native.ProcessPath(pid)) ? lastExternal : IntPtr.Zero; }
    void UpdateLabels()
    {
        var mod = shell.Tweaks.Module("clock-seconds");
        foreach (var item in shell.Config.TopDockItems) {
            var plugin = BarLayout.Component(item) == "plugin" ? shell.Mods.Catalog.FirstOrDefault(m => m.Id == item.Target) : null;
            if (plugin != null && elements.TryGetValue(item.Id, out var piece)) piece.Visibility = preview || PluginVisibility.Allowed(shell, plugin) ? Visibility.Visible : Visibility.Collapsed;
            if (labels.TryGetValue(item.Id, out var text)) text.Text = plugin != null ? PluginPresentation.Reading(shell, plugin, preview) : DockPieceText.Render(item, shell.Config, preview ? "Projects — Harbor Files" : activeTitle, preview ? 24 : shell.CpuUsage, preview ? 42 : shell.MemoryUsage, mod != null, mod == null ? null : shell.Mods.Value(mod, "format", "ddd, MMM d   h:mm:ss tt"));
        }
    }
    internal void PreparePreview() { preview = true; RefreshLayout(); }
    internal void SetFullscreen(bool hide)
    {
        bool primary = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Contains(Native.Monitor(MonitorHandle).Monitor.Left + 1, Native.Monitor(MonitorHandle).Monitor.Top + 1);
        hide |= !shell.Config.TopDockEnabled || shell.Config.TopDockPrimaryOnly && !primary;
        appbar?.SetOverlay(shell.Config.ReplaceTaskbar || !shell.Config.TopDockReserveSpace); appbar?.SetSuspended(hide);
        if (hide && IsVisible) Hide(); else if (!hide && !IsVisible) { Show(); appbar?.Position(); }
    }
}
