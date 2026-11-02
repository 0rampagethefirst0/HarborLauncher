using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Harbor;

internal sealed class PluginHandle : Window
{
    readonly Shell shell;
    readonly PluginControls owner;
    readonly ModDefinition mod;
    IntPtr monitor;
    readonly TextBlock text;
    readonly int index, total;
    readonly string mode;
    Point dragOrigin;
    bool armed, dragging;
    long ignoreClick;
    Rect placedWork;
    internal string ModId => mod.Id;
    internal IntPtr MonitorHandle => monitor;
    internal string AccessMode => mode;
    internal string Edge => PluginSnapping.Selected(shell.Mods, mod);
    internal bool HasSource => new WindowInteropHelper(this).Handle != IntPtr.Zero;
    internal Rect CurrentBounds => Bounds();
    internal void PlaceBounds(Rect bounds) => Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), (int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height, 0x14);
    internal PluginHandle(Shell shell, PluginControls owner, ModDefinition mod, IntPtr monitor, int index, int total)
    {
        this.shell = shell; this.owner = owner; this.mod = mod; this.monitor = monitor; this.index = index; this.total = total; mode = PluginPresentation.Mode(shell.Mods, mod);
        Title = "Harbor control — " + mod.Name; Width = shell.Mods.Number(mod, "control-width", 112, 44, 220); Height = shell.Mods.Number(mod, "control-size", 36, 30, 60);
        if (PluginPresentation.Content(shell.Mods, mod) == "Icon only") Width = Math.Max(44, Height + 8);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent;
        var frame = Face(shell, mod, Width, () => { if (Environment.TickCount64 < ignoreClick) return; owner.Open(mod, monitor, Bounds()); }, out var button, out text); Content = frame;
        button.ContextMenu = new ContextMenu(); button.ContextMenuOpening += (_, e) => { e.Handled = true; owner.Customize(mod, monitor, Bounds()); };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "plugin-control:" + mod.Id); System.Windows.Automation.AutomationProperties.SetName(button, "Open " + mod.Name);
        button.PreviewMouseLeftButtonDown += (_, e) => { dragOrigin = e.GetPosition(this); armed = true; };
        button.PreviewMouseLeftButtonUp += (_, _) => armed = false;
        button.PreviewMouseMove += (_, e) => {
            if (!armed || dragging || e.LeftButton != MouseButtonState.Pressed || (e.GetPosition(this) - dragOrigin).Length < 6) return;
            armed = false; dragging = true; ignoreClick = long.MaxValue; button.ReleaseMouseCapture();
            SnapGuides? guides = null;
            try { guides = new SnapGuides(shell, monitor); guides.Show(); DragMove(); SavePosition(); } finally { guides?.Close(); dragging = false; ignoreClick = Environment.TickCount64 + 300; }
        };
        SourceInitialized += (_, _) => { Native.ToolWindow(this); Position(); };
        WindowFade.Attach(this);
        UpdateReading();
    }
    internal static Border Face(Shell shell, ModDefinition mod, double width, Action open, out Button button, out TextBlock text, bool sample = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(UiIcons.Icon(PluginPresentation.Icon(mod), 17));
        text = Theme.Label(PluginPresentation.Content(shell.Mods, mod) == "Live reading" ? PluginPresentation.Reading(shell, mod, sample) : PluginPresentation.ShortName(mod), 12); text.Margin = new Thickness(7, 0, 0, 0); text.MaxWidth = Math.Max(1, width - 44); text.TextTrimming = TextTrimming.CharacterEllipsis; text.VerticalAlignment = VerticalAlignment.Center; text.Visibility = PluginPresentation.Content(shell.Mods, mod) == "Icon only" ? Visibility.Collapsed : Visibility.Visible; row.Children.Add(text);
        button = Theme.Button(row, mod.Name, open); button.Padding = new Thickness(9, 0, 7, 0); button.Background = Theme.Panel; button.HorizontalContentAlignment = HorizontalAlignment.Left; button.BorderThickness = new Thickness(0);
        return new Border { Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = button, ClipToBounds = true };
    }
    Rect Bounds() { Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var r); return new Rect(r.Left, r.Top, Math.Max(1, r.Width), Math.Max(1, r.Height)); }
    internal void Position()
    {
        if (dragging) return;
        var work = PluginSnapping.WorkArea(shell, monitor); double scale = Native.Scale(monitor); var area = work;
        placedWork = area;
        double height = Height * scale, block = Math.Min(work.Height - 12, total * (height + 8 * scale));
        double defaultY = work.Height <= height + 12 ? 0 : (Math.Max(0, (work.Height - block) / 2) + index * (height + 8 * scale)) / (work.Height - height - 12);
        string snap = PluginSnapping.Selected(shell.Mods, mod); if (mode == PluginPresentation.Floating && shell.Mods.Value(mod, "snap", "Auto") == "Auto") snap = "Free";
        var position = PluginSnapping.Place(area, new Size(Width * scale, height), snap, shell.Mods.Number(mod, "control-x", .82, 0, 1), shell.Mods.Number(mod, "control-y", defaultY, 0, 1));
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), (int)position.Left, (int)position.Top, (int)position.Width, (int)position.Height, 0x14);
    }
    void SavePosition()
    {
        var bounds = Bounds(); var nextMonitor = Native.MonitorFromWindow(new WindowInteropHelper(this).Handle, 2);
        if (nextMonitor != monitor && shell.Mods.Value(mod, "screen", "primary") != "all") { monitor = nextMonitor; shell.Mods.Set(mod, "screen", (Native.Monitor(monitor).Flags & 1) != 0 ? "primary" : "secondary", false); }
        var work = PluginSnapping.WorkArea(shell, monitor);
        string side = bounds.Left + bounds.Width / 2 < work.Left + work.Width / 2 ? "Left" : "Right";
        double x = Math.Clamp((bounds.Left - work.Left - 6) / Math.Max(1, work.Width - bounds.Width - 12), 0, 1), y = Math.Clamp((bounds.Top - work.Top - 6) / Math.Max(1, work.Height - bounds.Height - 12), 0, 1);
        shell.Mods.Set(mod, "control-x", x.ToString("R", CultureInfo.InvariantCulture), false); shell.Mods.Set(mod, "control-y", y.ToString("R", CultureInfo.InvariantCulture), false);
        shell.Mods.Set(mod, "snap", PluginSnapping.Nearest(new Rect(work.Left, work.Top, work.Width, work.Height), bounds, Native.Scale(monitor) * 36), false);
        if (mode == PluginPresentation.Edge) shell.Mods.Set(mod, "edge", side, false);
        dragging = false; Position(); owner.ReflowEdges(true);
    }
    internal void Tick()
    {
        if (owner.Live && new WindowInteropHelper(this).Handle != IntPtr.Zero) { var work = PluginSnapping.WorkArea(shell, monitor); if (placedWork != work) Position(); }
        UpdateReading(); UpdateVisibility();
    }
    void UpdateReading()
    {
        text.Visibility = PluginPresentation.Content(shell.Mods, mod) == "Icon only" ? Visibility.Collapsed : Visibility.Visible;
        text.Text = PluginPresentation.Content(shell.Mods, mod) == "Live reading" ? PluginPresentation.Reading(shell, mod) : PluginPresentation.ShortName(mod);
        if (text.Visibility == Visibility.Visible) { text.MaxWidth = 256; text.Measure(new Size(double.PositiveInfinity, Height)); double needed = Math.Clamp(Math.Ceiling((text.DesiredSize.Width + 44) / 16) * 16, shell.Mods.Number(mod, "control-width", 112, 44, 240), 300); text.MaxWidth = Math.Max(1, needed - 44); if (Math.Abs(Width - needed) > .5) { Width = needed; if (HasSource && !dragging) Position(); } }
        ToolTip = mod.Name + "\n" + PluginPresentation.Reading(shell, mod) + "\n" + PluginVisibility.Summary(shell, mod) + "\nClick to expand · drag to move · right-click for controls";
    }
    internal void UpdateVisibility()
    {
        if (!owner.Live) return;
        bool hide = !owner.Allowed(mod, monitor);
        WindowFade.Visible(this, !hide);
    }
}
