using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Harbor;

internal sealed class Dock : Window
{
    readonly Shell shell;
    internal readonly IntPtr MonitorHandle;
    readonly StackPanel items;
    readonly Border frame;
    readonly ScrollViewer scroller;
    readonly Button launcher, arrange, showDesktop;
    readonly Button controls;
    double opacityTarget = 1;
    readonly Border pinnedDivider = Divider(), runningDivider = Divider(), endDivider = Divider();
    readonly Dictionary<string, Border> customDividers = new();
    readonly Dictionary<string, DockApp> apps = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> runningOrder = new();
    ContextMenu? openMenu;
    bool dragging;
    long dragHoverUntil;
    Button? dropTarget;
    internal bool InputPassThrough { get; private set; }
    internal bool MenuOpen => dragging || Environment.TickCount64 < dragHoverUntil || openMenu?.IsOpen == true || ContextMenu?.IsOpen == true || apps.Values.Any(a => a.Button.ContextMenu?.IsOpen == true) || customDividers.Values.Any(d => d.ContextMenu?.IsOpen == true);
    AppBar? appbar;
    internal Dock(Shell shell, IntPtr monitor)
    {
        this.shell = shell; MonitorHandle = monitor;
        Title = "Harbor \u2014 dock"; Width = 400; Height = shell.Config.DockHeight;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent;
        items = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        scroller = new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        frame = new Border { Background = Theme.Bg, CornerRadius = new CornerRadius(shell.Config.DockRadius), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 0, 4), Child = scroller };
        var root = new Grid(); root.Children.Add(frame);
        controls = Theme.IconButton("\uE70E", "Dock controls — six settings", () => { Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var bounds); shell.OpenDockControls(MonitorHandle, bounds); });
        controls.Width = 32; controls.Height = 14; controls.MinHeight = 0; controls.MaxHeight = 14; controls.Padding = new Thickness(0); controls.HorizontalAlignment = HorizontalAlignment.Center; controls.VerticalAlignment = VerticalAlignment.Top; controls.Background = Theme.Raised; controls.Foreground = Theme.Accent; root.Children.Add(controls); Content = root;
        if (controls.Content is TextBlock glyph) glyph.FontSize = 10;
        launcher = Theme.Button(Theme.Logo(26), "Open apps \u2014 Windows key", () => shell.ToggleLauncher(MonitorHandle)); launcher.Width = 50;
        arrange = Theme.IconButton("\uE740", "Arrange windows", () => shell.OpenArrange(MonitorHandle)); arrange.Width = 40;
        showDesktop = Theme.IconButton("\uE7F4", "Show desktop and restore windows", shell.ToggleDesktop); showDesktop.Width = 40;
        SourceInitialized += (_, _) =>
        {
            Native.ToolWindow(this); appbar = new AppBar(this, monitor, 3, shell.Config.DockHeight, true, shell.Config.ReplaceTaskbar); Refresh(true);
            shell.DockInteraction.Register(this);
        };
        Closed += (_, _) => { shell.DockInteraction.Unregister(this); appbar?.Dispose(); };
        MouseEnter += (_, _) => shell.DockInteraction.Refresh();
        PreviewMouseDown += (_, _) => { if (!InputPassThrough) shell.DockInteraction.Touch(this); };
        PreviewKeyDown += (_, _) => shell.DockInteraction.Touch(this);
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Taskbar controls", () => { Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var bounds); shell.OpenDockControls(MonitorHandle, bounds); })); menu.Items.Add(Theme.Item("Dock controls (top)", () => shell.OpenTopDockControls(MonitorHandle))); menu.Items.Add(Theme.Item("Add taskbar divider", () => { shell.Config.TaskbarDividers.Add(new()); shell.Config.Save(); Refresh(); })); menu.Items.Add(Theme.Item("Apps", () => shell.ToggleLauncher(MonitorHandle))); menu.Items.Add(Theme.Item("Mods", shell.OpenMods)); menu.Items.Add(Theme.Item("Desktop profiles", shell.OpenProfiles)); menu.Items.Add(Theme.Item("Taskbar appearance", shell.OpenAppearance)); ContextMenu = menu;
        menu.Closed += (_, _) => shell.DockInteraction.Touch(this);
        PreviewMouseUp += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => Refresh()));
        PreviewMouseWheel += (_, e) => { var app = apps.Values.FirstOrDefault(a => a.Button.IsMouseOver); if (app != null && app.Wheel(e.Delta, (System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Alt) != 0)) { e.Handled = true; return; } if (scroller.ScrollableWidth > 0) { scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta * .6); e.Handled = true; } };
        menu.Items.Insert(3, Theme.Item("New app folder", () => { var folder = new AppFolder(); shell.Config.AppFolders.Add(folder); shell.SaveShortcuts(); shell.OpenAppFolder(folder, MonitorHandle); }));
        AllowDrop = true; root.AllowDrop = true; frame.AllowDrop = true; items.AllowDrop = true;
        root.PreviewDragOver += (_, e) => {
            if (!e.Data.GetDataPresent(TaskbarFolders.DragFormat) && !e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(BarLayout.DividerDragFormat)) { e.Effects = DragDropEffects.None; return; }
            dragHoverUntil = Environment.TickCount64 + 500; shell.DockInteraction.Touch(this); ClearDropCue();
            var (target, mode) = DropLocation(e); if (target != null) { dropTarget = target.Button; dropTarget.BorderBrush = Theme.Accent; dropTarget.BorderThickness = mode == "folder" ? new Thickness(2) : mode == "before" ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 3, 0); dropTarget.Background = Theme.Raised; }
            e.Effects = e.Data.GetDataPresent(TaskbarFolders.DragFormat) || e.Data.GetDataPresent(BarLayout.DividerDragFormat) ? DragDropEffects.Move : DragDropEffects.Link; e.Handled = true;
        };
        root.PreviewDragLeave += (_, e) => { var p = e.GetPosition(root); if (!new Rect(root.RenderSize).Contains(p)) { ClearDropCue(); dragHoverUntil = 0; } };
        root.PreviewDrop += (_, e) => {
            var (target, mode) = DropLocation(e); var sources = TaskbarFolders.Shortcuts(e.Data); var destination = target?.Shortcut;
            try {
                if (e.Data.GetData(BarLayout.DividerDragFormat) is string dividerId) { var divider = shell.Config.TaskbarDividers.FirstOrDefault(d => d.Id == dividerId); if (divider != null) { string anchor = target?.Anchor ?? ""; divider.Before = mode == "before" ? anchor : ""; divider.After = mode == "after" ? anchor : ""; shell.Config.Save(); } }
                else { var created = shell.DropTaskbarApps(sources, destination, mode); if (created != null && Settings.TestDataDir == null) Dispatcher.BeginInvoke(new Action(() => shell.OpenAppFolder(created, MonitorHandle))); }
            }
            catch (Exception ex) { shell.Notifications.Add("Apps could not be grouped", ex.Message); }
            ClearDropCue(); dragHoverUntil = 0; Dispatcher.BeginInvoke(new Action(() => Refresh())); e.Handled = true;
        };
    }
    (DockApp? Target, string Mode) DropLocation(DragEventArgs e)
    {
        foreach (var app in apps.Values) { var p = e.GetPosition(app.Button); if (!new Rect(app.Button.RenderSize).Contains(p)) continue;
            double fraction = p.X / Math.Max(1, app.Button.ActualWidth); var source = TaskbarFolders.Read(e.Data);
            bool folder = !e.Data.GetDataPresent(BarLayout.DividerDragFormat) && fraction is > .25 and < .75 && (source == null || !TaskbarFolders.IsFolder(source.Path) || TaskbarFolders.IsFolder(app.Shortcut.Path));
            return (app, folder ? "folder" : fraction < .5 ? "before" : "after");
        }
        return (null, "end");
    }
    void ClearDropCue() { if (dropTarget == null) return; dropTarget.ClearValue(BorderBrushProperty); dropTarget.ClearValue(BorderThicknessProperty); dropTarget.ClearValue(BackgroundProperty); dropTarget = null; }
    static Border Divider() => new() { Width = 1, Height = 26, Background = Theme.Line, Margin = new Thickness(6, 0, 6, 0) };
    internal void SetInputMode(bool passThrough, double fadedOpacity)
    {
        var h = new WindowInteropHelper(this).Handle;
        if (InputPassThrough != passThrough && h != IntPtr.Zero)
        {
            long style = Native.GetWindowLong(h, -20).ToInt64(); const long flags = 0x08000020;
            InputPassThrough = passThrough; IsHitTestVisible = !passThrough;
            Native.SetWindowLong(h, -20, new IntPtr(passThrough ? style | flags : style & ~flags));
        }
        double desired = passThrough ? fadedOpacity : 1;
        if (Math.Abs(opacityTarget - desired) > .001)
        {
            double from = Opacity; opacityTarget = desired; BeginAnimation(OpacityProperty, null); Opacity = desired;
            if (IsVisible && Settings.TestDataDir == null && shell.Config.DockMotionMilliseconds > 0) BeginAnimation(OpacityProperty, new DoubleAnimation(from, desired, TimeSpan.FromMilliseconds(shell.Config.DockMotionMilliseconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    internal void Refresh(bool force = false)
    {
        if (IsMouseCaptureWithin || (IsMouseOver && (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed || Mouse.MiddleButton == MouseButtonState.Pressed)) || MenuOpen) return;
        Height = shell.Config.DockHeight; frame.CornerRadius = new CornerRadius(shell.Config.DockRadius); appbar?.Configure(shell.Config.DockHeight, shell.Config.DockAlignment);
        double buttonHeight = shell.Config.IconSize + 14; launcher.Height = arrange.Height = showDesktop.Height = buttonHeight;
        var desired = new List<UIElement> { launcher, pinnedDivider }; var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windows = shell.Tracker.Windows.Where(w => !shell.Tweaks.Has("dock-monitor-only") || w.Monitor == MonitorHandle).ToList();
        foreach (var pin in shell.Config.Pins)
        {
            foreach (var separator in shell.Config.TaskbarDividers.Where(s => s.Before.Equals(pin.Path, StringComparison.OrdinalIgnoreCase))) AddDivider(separator);
            var folder = TaskbarFolders.Find(shell.Config, pin.Path);
            string token = "pin:" + pin.Path; var group = windows.Where(w => folder == null ? w.Matches(pin) : folder.Apps.Any(w.Matches)).ToList();
            Add(token, pin.Name, pin.Path, pin, pin.Target, group); foreach (var w in group) included.Add(w.Key);
            foreach (var separator in shell.Config.TaskbarDividers.Where(s => s.After.Equals(pin.Path, StringComparison.OrdinalIgnoreCase))) AddDivider(separator);
        }
        var running = windows.Where(w => w.Monitor == MonitorHandle && !included.Contains(w.Key)).GroupBy(w => w.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var anchors = shell.Config.Pins.Select(p => p.Path).Concat(running.Keys.Select(k => "run:" + k)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var separator in shell.Config.TaskbarDividers.Where(s => s.Before.Length == 0 && s.After.Length == 0 || !anchors.Contains(s.Before.Length > 0 ? s.Before : s.After))) AddDivider(separator);
        runningOrder.RemoveAll(key => !running.ContainsKey(key));
        foreach (string key in running.Keys.Where(key => !runningOrder.Contains(key, StringComparer.OrdinalIgnoreCase)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase)) runningOrder.Add(key);
        if (running.Count > 0) desired.Add(runningDivider);
        foreach (string key in runningOrder)
        {
            foreach (var separator in shell.Config.TaskbarDividers.Where(s => s.Before.Equals("run:" + key, StringComparison.OrdinalIgnoreCase))) AddDivider(separator);
            var group = running[key]; var first = group[0];
            string name = shell.Catalog.Apps.FirstOrDefault(a => Shell.KeysMatch(a.Target, first.Key))?.Name ?? (first.AppId.Length > 0 ? first.Title : first.ProcessName);
            Add("run:" + key, name, first.LaunchPath, null, key, group);
            foreach (var separator in shell.Config.TaskbarDividers.Where(s => s.After.Equals("run:" + key, StringComparison.OrdinalIgnoreCase))) AddDivider(separator);
        }
        desired.Add(endDivider); desired.Add(arrange); desired.Add(showDesktop);
        // Reconcile only changed positions. Existing buttons keep mouse capture, focus and UIA identity.
        for (int i = 0; i < desired.Count; i++)
        {
            if (i < items.Children.Count && ReferenceEquals(items.Children[i], desired[i])) continue;
            if (items.Children.Contains(desired[i])) items.Children.Remove(desired[i]); items.Children.Insert(i, desired[i]);
        }
        while (items.Children.Count > desired.Count) items.Children.RemoveAt(items.Children.Count - 1);
        foreach (string key in apps.Keys.Where(key => !keep.Contains(key)).ToArray()) apps.Remove(key);
        items.Measure(new Size(double.PositiveInfinity, buttonHeight + 8));
        double width = Math.Min(items.DesiredSize.Width + 2, Native.Monitor(MonitorHandle).Monitor.Width / Native.Scale(MonitorHandle) - 24);
        if (Math.Abs(Width - width) > .5) { Width = width; appbar?.SetWidth(width); }
        UpdateActive();
        void Add(string token, string name, string path, Pin? pin, string groupKey, List<WindowEntry> group)
        {
            if (!apps.TryGetValue(token, out var app)) apps[token] = app = new DockApp(this, token);
            app.Update(name, path, pin, groupKey, group); desired.Add(app.Button); keep.Add(token);
        }
        void AddDivider(TaskbarDivider definition)
        {
            if (!customDividers.TryGetValue(definition.Id, out var divider)) {
                divider = Divider(); customDividers[definition.Id] = divider; divider.ToolTip = "Taskbar divider · right-click to edit";
                var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Switch line / space", () => { definition.Space = !definition.Space; shell.Config.Save(); Refresh(); })); menu.Items.Add(Theme.Item("Remove divider", () => { shell.Config.TaskbarDividers.Remove(definition); shell.Config.Save(); Refresh(); })); menu.Closed += (_, _) => Dispatcher.BeginInvoke(new Action(() => Refresh())); divider.ContextMenu = menu;
                AppDrag.BindData(divider, () => new DataObject(BarLayout.DividerDragFormat, definition.Id), value => { dragging = value; shell.DockInteraction.Touch(this); if (!value) Dispatcher.BeginInvoke(new Action(() => Refresh())); }); divider.Cursor = Cursors.SizeWE;
            }
            divider.Width = definition.Space ? 16 : 1; divider.Background = definition.Space ? Brushes.Transparent : Theme.Line; desired.Add(divider);
        }
    }
    void UpdateActive()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        foreach (var app in apps.Values) app.SetActive(foreground);
    }
    internal void Tick()
    {
        Refresh(); bool hide = shell.HideBars(MonitorHandle); appbar?.SetOverlay(shell.Config.ReplaceTaskbar); appbar?.SetSuspended(hide);
        if (hide && IsVisible) Hide(); else if (!hide && !IsVisible) { Show(); appbar?.Position(); }
    }
    sealed class DockApp
    {
        readonly Dock dock;
        readonly Border indicator = new() { Height = 3, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom };
        readonly TextBlock caption = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 10, 0) };
        internal readonly Button Button;
        internal string Anchor => pin?.Path ?? "run:" + groupKey;
        internal Pin Shortcut => pin ?? new Pin { Name = name, Path = path, Target = groupKey };
        Pin? pin;
        string name = "", path = "", groupKey = "", iconSpec = "";
        List<WindowEntry> windows = new();
        internal DockApp(Dock dock, string token)
        {
            this.dock = dock; Button = Theme.Button("", token, Click); AutomationProperties.SetAutomationId(Button, token);
            var lift = new TranslateTransform(); Button.RenderTransform = lift;
            Button.MouseEnter += (_, _) => Animate(-3); Button.MouseLeave += (_, _) => Animate(0);
            Button.Padding = new Thickness(3, 0, 3, 0); Button.Margin = new Thickness(1, 0, 1, 0); Button.ContextMenu = new ContextMenu();
            Button.ContextMenu.Closed += (_, _) => { dock.shell.DockInteraction.Touch(dock); dock.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => dock.Refresh())); };
            Button.ContextMenuOpening += (_, _) => Menu(Button.ContextMenu!);
            AppDrag.Bind(Button, () => Shortcut, value => { dock.dragging = value; dock.shell.DockInteraction.Touch(dock); if (!value) dock.Dispatcher.BeginInvoke(new Action(() => dock.Refresh())); });
            Button.PreviewMouseDown += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                if (dock.shell.Tweaks.Has("dock-middle-close")) { var before = Native.ForegroundBeforeClick(dock); var current = Current(); var target = current.FirstOrDefault(w => w.Handle == before) ?? current.FirstOrDefault(); if (target != null) Native.PostMessage(target.Handle, 0x10, IntPtr.Zero, IntPtr.Zero); e.Handled = true; }
                else if (pin != null) { dock.shell.Launch(pin.Path); e.Handled = true; }
            };
            void Animate(double y) { int ms = dock.shell.Config.DockMotionMilliseconds; if (ms <= 0 || Settings.TestDataDir != null || !dock.IsVisible) { lift.BeginAnimation(TranslateTransform.YProperty, null); lift.Y = y; return; } double from = lift.Y; lift.BeginAnimation(TranslateTransform.YProperty, null); lift.Y = y; lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from, y, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop }); }
        }
        internal void Update(string name, string path, Pin? pin, string key, List<WindowEntry> current)
        {
            this.name = name; this.path = path; this.pin = pin; groupKey = key; windows = current;
            string label = name + (current.Count > 0 ? $" ({current.Count} open)" : ""); Button.ToolTip = label; AutomationProperties.SetName(Button, label);
            var labels = dock.shell.Tweaks.Module("dock-labels"); bool labeled = labels != null && (current.Count > 0 || dock.shell.Mods.Value(labels, "when", "always") == "always");
            int labelWidth = labels == null ? 0 : (int)dock.shell.Mods.Number(labels, "width", 130, 50, 240);
            var folder = TaskbarFolders.Find(dock.shell.Config, path);
            int size = dock.shell.Config.IconSize; string spec = path + "|" + name + "|" + size + "|" + labeled + "|" + labelWidth + "|" + (folder == null ? "" : string.Join("|", folder.Apps.Select(p => p.Path)));
            caption.Text = labels != null && dock.shell.Mods.Value(labels, "label", "app") == "window" ? (current.FirstOrDefault(w => w.Handle == Native.GetForegroundWindow()) ?? current.FirstOrDefault())?.Title ?? name : name;
            if (spec != iconSpec)
            {
                iconSpec = spec;
                if (Button.Content is Panel old) { old.Children.Remove(indicator); old.Children.Remove(caption); }
                var grid = new Grid { Width = size + 16 + (labeled ? labelWidth : 0), Height = size + 14 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(size + 16) }); if (labeled) grid.ColumnDefinitions.Add(new ColumnDefinition());
                var icon = folder == null ? Theme.AppIcon(path, name, size) : FolderUi.Icon(folder, size); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 3, 0, 0); grid.Children.Add(icon); grid.Children.Add(indicator); Button.Content = grid;
                if (labeled) { Grid.SetColumn(caption, 1); grid.Children.Add(caption); }
            }
            Button.Height = size + 14; indicator.Visibility = current.Count > 0 ? Visibility.Visible : Visibility.Hidden;
        }
        List<WindowEntry> Current() { var folder = TaskbarFolders.Find(dock.shell.Config, path); return dock.shell.Tracker.Windows.Where(w => (!dock.shell.Tweaks.Has("dock-monitor-only") || w.Monitor == dock.MonitorHandle) && (folder != null ? folder.Apps.Any(w.Matches) : pin != null ? w.Matches(pin) : w.Key.Equals(groupKey, StringComparison.OrdinalIgnoreCase))).ToList(); }
        internal bool Wheel(int delta, bool alt)
        {
            var current = Current();
            if (alt && dock.shell.Tweaks.Module("dock-wheel-volume") is { } volume)
            {
                var pids = current.Select(w => { Native.GetWindowThreadProcessId(w.Handle, out uint pid); return pid; }).Where(pid => pid != 0).Distinct().ToList();
                var paths = current.Select(w => w.Path).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                int changed = Audio.AdjustApp(pids, paths, Math.Sign(delta) * (float)dock.shell.Mods.Number(volume, "step", 3, 1, 20) / 100);
                Button.ToolTip = changed == 0 ? name + " · no active audio session" : name + " · app volume " + (delta > 0 ? "increased" : "decreased"); return true;
            }
            if (dock.shell.Tweaks.Module("dock-scroll-windows") is not { } cycle) return false;
            current = current.Where(w => dock.shell.Mods.Value(cycle, "minimized", "yes") == "yes" || !Native.IsIconic(w.Handle)).OrderBy(w => w.Handle.ToInt64()).ToList();
            if (current.Count == 0) return false;
            int index = current.FindIndex(w => w.Handle == Native.GetForegroundWindow()), direction = Math.Sign(delta) * (dock.shell.Mods.Value(cycle, "direction", "normal") == "normal" ? -1 : 1);
            Native.Activate(current[(index < 0 ? 0 : (index + direction + current.Count) % current.Count)].Handle); return true;
        }
        void Click()
        {
            if (AppDrag.Suppressed) return;
            if (TaskbarFolders.Find(dock.shell.Config, path) is { } folder) { dock.shell.OpenAppFolder(folder, dock.MonitorHandle); return; }
            var current = Current();
            if (current.Count == 0) { if (pin != null) dock.shell.Launch(pin.Path); return; }
            if (current.Count == 1) { Native.Activate(current[0].Handle, true, Native.ForegroundBeforeClick(dock)); return; }
            var menu = new ContextMenu(); foreach (var w in current) menu.Items.Add(Theme.Item(w.Title, () => Native.Activate(w.Handle)));
            dock.openMenu = menu; menu.Closed += (_, _) => { dock.openMenu = null; dock.Refresh(); }; menu.PlacementTarget = Button; menu.IsOpen = true;
        }
        void Menu(ContextMenu menu)
        {
            if (TaskbarFolders.Find(dock.shell.Config, path) is { } folder) { menu.Items.Clear(); var entries = FolderUi.Menu(dock.shell, folder, dock); foreach (var entry in entries.Items.Cast<object>().ToArray()) { entries.Items.Remove(entry); menu.Items.Add(entry); } return; }
            menu.Items.Clear(); var current = Current(); string exe = current.FirstOrDefault()?.Path ?? "";
            if (exe.Length > 0) { var game = Theme.Item("Treat as game", () => dock.shell.ToggleGame(exe)); game.IsCheckable = true; game.IsChecked = dock.shell.Games.IsGame(exe); menu.Items.Add(game); }
            if (current.FirstOrDefault() is { } window) menu.Items.Add(Theme.Item("Mods for this app", () => dock.shell.OpenAppMods(window)));
            if (pin != null)
            {
                menu.Items.Add(Theme.Item("Open new window", () => dock.shell.Launch(pin.Path))); menu.Items.Add(Theme.Item("Unpin", () => dock.shell.Unpin(pin)));
                menu.Items.Add(Theme.Item("Move left", () => dock.shell.MovePin(pin, -1))); menu.Items.Add(Theme.Item("Move right", () => dock.shell.MovePin(pin, 1)));
                menu.Items.Add(Theme.Item("Add divider before", () => { dock.shell.Config.TaskbarDividers.Add(new TaskbarDivider { Before = pin.Path }); dock.shell.Config.Save(); dock.Refresh(); }));
                menu.Items.Add(Theme.Item("Add divider after", () => { dock.shell.Config.TaskbarDividers.Add(new TaskbarDivider { After = pin.Path }); dock.shell.Config.Save(); dock.Refresh(); }));
            }
            else if (path.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase) || File.Exists(path)) menu.Items.Add(Theme.Item("Pin to dock", () => dock.shell.Pin(new Pin { Name = name, Path = path, Target = groupKey })));
            if (pin == null && current.Count > 0) {
                menu.Items.Add(Theme.Item("Add divider before", () => { dock.shell.Config.TaskbarDividers.Add(new TaskbarDivider { Before = "run:" + groupKey }); dock.shell.Config.Save(); dock.Refresh(); }));
                menu.Items.Add(Theme.Item("Add divider after", () => { dock.shell.Config.TaskbarDividers.Add(new TaskbarDivider { After = "run:" + groupKey }); dock.shell.Config.Save(); dock.Refresh(); }));
            }
            if (current.Count > 0)
            {
                menu.Items.Add(new Separator()); foreach (var w in current) menu.Items.Add(Theme.Item(w.Title.Length > 70 ? w.Title[..70] + "..." : w.Title, () => Native.Activate(w.Handle)));
                menu.Items.Add(Theme.Item(current.Count == 1 ? "Close window" : "Close windows", () => { foreach (var w in Current()) Native.PostMessage(w.Handle, 0x10, IntPtr.Zero, IntPtr.Zero); }));
                if (current.All(w => w.ProcessName is not "ApplicationFrameHost" and not "Harbor Files" && !w.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))) menu.Items.Add(Theme.Item("End task", () => { var targets = Current().Select(w => { Native.GetWindowThreadProcessId(w.Handle, out uint p); return (Pid: p, w.Path); }).Where(t => t.Pid != 0 && t.Pid != Environment.ProcessId && t.Path.Length > 0 && Native.ProcessPath(t.Pid).Equals(t.Path, StringComparison.OrdinalIgnoreCase)).Distinct(); foreach (var target in targets) try { using var process = System.Diagnostics.Process.GetProcessById((int)target.Pid); process.Kill(); } catch (Exception ex) { dock.shell.Notifications.Add("App could not be ended", ex.Message); } }));
            }
        }
        internal void SetActive(IntPtr active) { bool selected = windows.Any(w => w.Handle == active); indicator.Background = selected ? Theme.Accent : Theme.Muted; indicator.Width = selected ? 20 : windows.Count > 1 ? 18 : 5; }
    }
}
