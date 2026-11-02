using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Harbor;

internal sealed class PluginControls : IDisposable
{
    readonly Shell shell;
    readonly bool live;
    readonly Dictionary<string, Widget> widgets = new();
    readonly List<PluginHandle> handles = new();
    PluginPopup? popup;
    PluginQuickSettings? controls;
    readonly Dictionary<string, PluginPopup> persistent = new();
    string accessSignature = "";
    string AccessSignature => string.Join("|", shell.Mods.Enabled.Where(m => m.Kind == "widget").Select(m => m.Id + ":" + PluginPresentation.Mode(shell.Mods, m)));
    internal int HandleCount => handles.Count;
    internal int VisibleHandleCount => handles.Count(h => h.IsVisible);
    internal int BackgroundCount => widgets.Count;
    internal string? OpenTool => popup?.ModId;
    internal int AlwaysOnCount => persistent.Count;
    internal int VisibleAlwaysOnCount => persistent.Values.Count(p => p.IsVisible);
    internal bool Live => live && Settings.TestDataDir == null;
    internal bool Allowed(ModDefinition mod, IntPtr monitor) => PluginVisibility.Allowed(shell, mod) && !shell.HideBars(monitor);
    internal void Customize(ModDefinition mod, IntPtr monitor, Rect? anchor = null)
    {
        controls?.Close(); controls = new PluginQuickSettings(shell, mod, monitor, anchor);
        var next = controls; next.Closed += (_, _) => { if (controls == next) controls = null; };
        next.Show(); Native.Activate(new WindowInteropHelper(next).Handle);
    }
    internal PluginControls(Shell shell, bool live = false) { this.shell = shell; this.live = live; }
    internal void Rebuild()
    {
        accessSignature = AccessSignature;
        ClosePanel();
        foreach (var handle in handles) handle.Close(); handles.Clear(); foreach (var widget in widgets.Values) widget.Flush(); widgets.Clear();
        var mods = shell.Mods.Enabled.Where(m => m.Kind == "widget" && !PluginPresentation.IsDesktop(shell.Mods, m)).ToArray();
        foreach (var mod in mods) widgets[mod.Id] = new Widget(shell, mod, inline: true);
        PluginPresentation.SyncDock(shell);
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
            var candidates = mods.Where(m => PluginPresentation.Mode(shell.Mods, m) is PluginPresentation.Edge or PluginPresentation.Floating && OnScreen(m, screen.Primary)).ToArray();
            for (int i = 0; i < candidates.Length; i++)
            {
                var mod = candidates[i]; var handle = new PluginHandle(shell, this, mod, monitor, i, candidates.Length); handles.Add(handle);
                if (Live && Allowed(mod, monitor)) handle.Show();
            }
        }
        ReflowEdges(); Tick();
    }
    bool OnScreen(ModDefinition mod, bool primary) => shell.Mods.Value(mod, "screen", "primary") switch { "all" => true, "secondary" => !primary, _ => primary };
    internal void Tick()
    {
        // Timers, CPU history and clipboard capture keep working while collapsed.
        // No animation or rendering loop runs for a hidden full panel.
        foreach (var widget in widgets.Values) widget.Tick();
        if (popup != null && !widgets.Values.Contains(popup.Widget)) popup.Widget.Tick();
        foreach (var handle in handles) handle.Tick();
        popup?.RefreshReading();
        if (popup is { Pinned: true }) { persistent[popup.ModId] = popup; popup = null; }
        foreach (var panel in persistent.Values.ToArray()) panel.RefreshReading();
        UpdateVisibility();
        if (Live) foreach (var mod in shell.Mods.Enabled.Where(m => m.Kind == "widget" && !PluginPresentation.IsDesktop(shell.Mods, m) && shell.Mods.Value(m, "always-on", "No") == "Yes").ToArray())
        {
            if (persistent.ContainsKey(mod.Id) || popup?.ModId == mod.Id) continue;
            var handle = handles.FirstOrDefault(h => h.ModId == mod.Id); var monitor = handle?.MonitorHandle ?? shell.CurrentMonitor();
            if (Allowed(mod, monitor)) CreatePanel(mod, monitor, handle?.HasSource == true ? handle.CurrentBounds : null);
        }
    }
    internal void UpdateVisibility()
    {
        foreach (var handle in handles) handle.UpdateVisibility();
        if (popup != null && !Allowed(popup.Definition, popup.MonitorHandle)) popup.RequestClose();
        foreach (var panel in persistent.Values.ToArray()) { bool hidden = !Allowed(panel.Definition, panel.MonitorHandle); if (Live) WindowFade.Visible(panel, !hidden); }
    }
    internal void RebuildIfAccessChanged() { if (accessSignature != AccessSignature) Rebuild(); }
    internal void ReflowEdges(bool save = false)
    {
        foreach (var group in handles.Where(h => h.HasSource && h.AccessMode == PluginPresentation.Edge && h.Edge != "Free").GroupBy(h => (h.MonitorHandle, h.Edge)))
        {
            var members = group.ToArray(); var area = PluginSnapping.WorkArea(shell, group.Key.MonitorHandle); bool horizontal = group.Key.Edge.StartsWith("Top") || group.Key.Edge.StartsWith("Bottom"); var desired = members.Select(h => h.CurrentBounds).ToArray();
            var packed = horizontal ? PluginPresentation.PackVertical(desired.Select(r => new Rect(r.Top, r.Left, r.Height, r.Width)).ToArray(), new Rect(area.Top, area.Left, area.Height, area.Width), Native.Scale(group.Key.MonitorHandle) * 8).Select(r => new Rect(r.Top, r.Left, r.Height, r.Width)).ToArray() : PluginPresentation.PackVertical(desired, area, Native.Scale(group.Key.MonitorHandle) * 8);
            for (int i = 0; i < members.Length; i++)
            {
                members[i].PlaceBounds(packed[i]);
                if (save) { if (!shell.Config.ModOptions.TryGetValue(members[i].ModId, out var values)) shell.Config.ModOptions[members[i].ModId] = values = new(); values["control-y"] = Math.Clamp((packed[i].Top - area.Top - 6) / Math.Max(1, area.Height - packed[i].Height - 12), 0, 1).ToString("R", CultureInfo.InvariantCulture); }
            }
        }
        if (save) shell.Config.Save();
    }
    internal void ClosePanel() { popup?.RequestClose(); popup = null; foreach (var panel in persistent.Values.ToArray()) panel.RequestClose(); persistent.Clear(); }
    internal void Open(ModDefinition mod, IntPtr monitor, Rect? anchor = null)
    {
        if (!shell.Mods.EnabledMod(mod) || !PluginVisibility.Allowed(shell, mod)) return;
        if (persistent.TryGetValue(mod.Id, out var pinned)) { pinned.UserClose(); return; }
        if (popup is { IsVisible: true } && popup.ModId == mod.Id) { popup.UserClose(); return; }
        if (popup is { Pinned: true }) { persistent[popup.ModId] = popup; popup = null; } else { popup?.RequestClose(); popup = null; }
        CreatePanel(mod, monitor, anchor);
    }
    void CreatePanel(ModDefinition mod, IntPtr monitor, Rect? anchor)
    {
        if (!widgets.TryGetValue(mod.Id, out var widget)) widget = new Widget(shell, mod, inline: true);
        var next = new PluginPopup(shell, mod, widget, monitor, anchor);
        if (next.Pinned) persistent[mod.Id] = next; else popup = next;
        next.Closed += (_, _) => { if (popup == next) popup = null; if (persistent.GetValueOrDefault(mod.Id) == next) persistent.Remove(mod.Id); };
        shell.ShowPluginPanel(next);
    }
    internal ContextMenu Menu(ModDefinition mod, IntPtr monitor, Rect? anchor = null)
    {
        var menu = new ContextMenu();
        var info = new StackPanel { Margin = new Thickness(10, 6, 10, 8), MaxWidth = 300 };
        info.Children.Add(Theme.Label(mod.Name, 15));
        info.Children.Add(new TextBlock { Text = PluginVisibility.Summary(shell, mod), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        menu.Items.Add(new MenuItem { Header = info, IsEnabled = false });
        menu.Items.Add(Theme.Item("Control settings…", () => Customize(mod, monitor, anchor)));
        menu.Items.Add(Theme.Item("Open tool", () => Open(mod, monitor, anchor)));
        menu.Items.Add(Theme.Item("Plugin settings", () => shell.OpenPluginSettings(mod)));
        return menu;
    }
    public void Dispose() { controls?.Close(); ClosePanel(); foreach (var handle in handles) handle.Close(); handles.Clear(); foreach (var widget in widgets.Values) widget.Flush(); widgets.Clear(); }
}

internal sealed class PluginPopup : Window
{
    readonly Shell shell;
    readonly ModDefinition mod;
    readonly TextBlock reading;
    readonly ContentControl holder;
    internal Widget Widget { get; }
    bool closing;
    internal bool Pinned { get; private set; }
    internal string ModId => mod.Id;
    internal ModDefinition Definition => mod;
    readonly IntPtr originalMonitor;
    internal IntPtr MonitorHandle => new WindowInteropHelper(this).Handle is var handle && handle != IntPtr.Zero ? Native.MonitorFromWindow(handle, 2) : originalMonitor;
    internal PluginPopup(Shell shell, ModDefinition mod, Widget widget, IntPtr monitor, Rect? anchor = null)
    {
        this.shell = shell; this.mod = mod; originalMonitor = monitor; Widget = widget; Pinned = shell.Mods.Value(mod, "always-on", "No") == "Yes";
        Title = "Harbor — " + mod.Name;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        AllowsTransparency = true; Background = Brushes.Transparent;
        var work = PluginSnapping.WorkArea(shell, monitor); double scale = Native.Scale(monitor);
        Width = Math.Min(widget.View.Width + 24, work.Width / scale - 20);
        var root = new DockPanel { Margin = new Thickness(8) };
        var header = new DockPanel { Margin = new Thickness(8, 4, 2, 4) };
        var close = Theme.Button(UiIcons.Icon("close", 14), "Collapse " + mod.Name, UserClose); close.Padding = new Thickness(7); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close);
        Button pin = null!; pin = Theme.Button(UiIcons.Icon("pin", 14), "Toggle persistent always-on mode", () => { Pinned = !Pinned; shell.Mods.Set(mod, "always-on", Pinned ? "Yes" : "No", false); if (Pinned) shell.DetachPluginPanel(this); PaintPin(); }); pin.Padding = new Thickness(7); DockPanel.SetDock(pin, System.Windows.Controls.Dock.Right); header.Children.Add(pin);
        var settings = Theme.Button(UiIcons.Icon("controls", 14), "Settings for " + mod.Name, () => shell.Plugins.Customize(mod, MonitorHandle)); settings.Padding = new Thickness(7); DockPanel.SetDock(settings, System.Windows.Controls.Dock.Right); header.Children.Add(settings);
        var title = Theme.Label(mod.Name, 16); title.FontWeight = FontWeights.SemiBold; title.TextTrimming = TextTrimming.CharacterEllipsis; title.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(title);
        title.ToolTip = "Drag to move this tool"; title.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) try { DragMove(); } catch (InvalidOperationException) { } };
        DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        reading = Theme.Label("", 12, Theme.Muted); reading.TextWrapping = TextWrapping.Wrap; reading.Margin = new Thickness(8, 6, 8, 8); DockPanel.SetDock(reading, System.Windows.Controls.Dock.Bottom); root.Children.Add(reading);
        holder = new ContentControl { Content = widget.View, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Stretch }; root.Children.Add(holder);
        var frame = Theme.Card(root, 14); frame.Background = Theme.Panel; Content = frame;
        RefreshReading(); frame.Measure(new Size(Width, Math.Max(100, work.Height / scale - 24)));
        Height = Math.Clamp(frame.DesiredSize.Height, 140, Math.Max(140, work.Height / scale - 24));
        SourceInitialized += (_, _) => {
            Native.ToolWindow(this); double width = Width * scale, height = Height * scale;
            double left = anchor is Rect a ? a.Left - width - 10 : work.Right - width - 12;
            if (anchor is Rect b && b.Left < work.Left + work.Width / 2) left = b.Right + 10;
            double top = anchor?.Top ?? work.Top + 14;
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), (int)Math.Clamp(left, work.Left + 8, Math.Max(work.Left + 8, work.Right - width - 8)), (int)Math.Clamp(top, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - height - 8)), (int)width, (int)height, 0x10);
        };
        WindowFade.Attach(this);
        Closing += (_, _) => closing = true;
        Closed += (_, _) => { closing = true; holder.Content = null; };
        Deactivated += (_, _) => { if (!Pinned) RequestClose(); };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { UserClose(); e.Handled = true; } };
        RefreshReading(); PaintPin();
        void PaintPin() { pin.Background = Pinned ? Theme.Raised : Brushes.Transparent; pin.Foreground = Pinned ? Theme.Accent : Theme.Text; pin.ToolTip = Pinned ? "Pinned open · click to restore automatic collapse" : "Keep this tool open"; }
    }
    internal void RefreshReading() => reading.Text = Pinned ? "Kept expanded · " + PluginVisibility.Summary(shell, mod) : "Click away to collapse · right-click its control for settings";
    internal void RequestClose() { if (closing) return; closing = true; Close(); }
    internal void UserClose() { if (Pinned) { Pinned = false; shell.Mods.Set(mod, "always-on", "No", false); } RequestClose(); }
}
