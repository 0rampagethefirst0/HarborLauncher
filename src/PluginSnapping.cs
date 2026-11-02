using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Harbor;

internal static class PluginSnapping
{
    internal static Rect WorkArea(Shell shell, IntPtr monitor)
    {
        var info = Native.Monitor(monitor); var work = info.Work; double scale = Native.Scale(monitor); bool primary = (info.Flags & 1) != 0, bars = primary || shell.Config.AllMonitors && ShellFeatures.On(shell.Config, "multi-monitor");
        if (bars && ShellFeatures.On(shell.Config, "top-dock") && shell.Config.TopDockEnabled && (!shell.Config.TopDockPrimaryOnly || primary)) work.Top = Math.Max(work.Top, info.Monitor.Top + (int)Math.Round(shell.Config.TopBarHeight * scale));
        if (bars && ShellFeatures.On(shell.Config, "taskbar")) work.Bottom = Math.Min(work.Bottom, info.Monitor.Bottom - (int)Math.Round((shell.Config.DockHeight + 6) * scale));
        return new Rect(work.Left, work.Top, Math.Max(1, work.Width), Math.Max(1, work.Height));
    }
    internal static readonly string[] Positions = { "Auto", "Center box: upper left", "Center box: upper right", "Center box: lower left", "Center box: lower right", "Top left", "Top center", "Top right", "Right center", "Bottom right", "Bottom center", "Bottom left", "Left center", "Left", "Right", "Top", "Bottom", "Free" };
    internal static string Default(ModDefinition mod) => PluginPresentation.Key(mod) switch { "clock" => Positions[1], "system" => Positions[2], "folders" => Positions[3], "storage" => Positions[4], _ => "Right" };
    internal static string Selected(Mods mods, ModDefinition mod) { string value = mods.Value(mod, "snap", "Auto"); return value == "Auto" || !Positions.Contains(value) ? Default(mod) : value; }
    internal static Rect CenterBox(Rect work) => new(work.Left + work.Width * .20, work.Top + work.Height * .20, work.Width * .60, work.Height * .60);
    internal static Rect Place(Rect work, Size size, string position, double x, double y)
    {
        var box = CenterBox(work); double px = work.Left + 6 + x * Math.Max(0, work.Width - size.Width - 12), py = work.Top + 6 + y * Math.Max(0, work.Height - size.Height - 12);
        switch (position)
        {
            case "Center box: upper left": px = box.Left - size.Width / 2; py = box.Top - size.Height / 2; break;
            case "Center box: upper right": px = box.Right - size.Width / 2; py = box.Top - size.Height / 2; break;
            case "Center box: lower left": px = box.Left - size.Width / 2; py = box.Bottom - size.Height / 2; break;
            case "Center box: lower right": px = box.Right - size.Width / 2; py = box.Bottom - size.Height / 2; break;
            case "Top left": px = work.Left + 6; py = work.Top + 6; break;
            case "Top center": px = work.Left + (work.Width - size.Width) / 2; py = work.Top + 6; break;
            case "Top right": px = work.Right - size.Width - 6; py = work.Top + 6; break;
            case "Right center": px = work.Right - size.Width - 6; py = work.Top + (work.Height - size.Height) / 2; break;
            case "Bottom right": px = work.Right - size.Width - 6; py = work.Bottom - size.Height - 6; break;
            case "Bottom center": px = work.Left + (work.Width - size.Width) / 2; py = work.Bottom - size.Height - 6; break;
            case "Bottom left": px = work.Left + 6; py = work.Bottom - size.Height - 6; break;
            case "Left center": px = work.Left + 6; py = work.Top + (work.Height - size.Height) / 2; break;
            case "Left": px = work.Left + 6; break; case "Right": px = work.Right - size.Width - 6; break;
            case "Top": py = work.Top + 6; break; case "Bottom": py = work.Bottom - size.Height - 6; break;
        }
        return new Rect(Math.Clamp(px, work.Left + 6, Math.Max(work.Left + 6, work.Right - size.Width - 6)), Math.Clamp(py, work.Top + 6, Math.Max(work.Top + 6, work.Bottom - size.Height - 6)), size.Width, size.Height);
    }
    internal static string Nearest(Rect work, Rect actual, double threshold)
    {
        var size = actual.Size; string best = "Free"; double distance = threshold;
        foreach (string position in Positions.Skip(1).Take(12)) { var target = Place(work, size, position, .5, .5); double next = (target.Location - actual.Location).Length; if (next <= distance) { distance = next; best = position; } }
        if (best != "Free") return best;
        var edges = new[] { ("Left", Math.Abs(actual.Left - work.Left - 6)), ("Right", Math.Abs(work.Right - actual.Right - 6)), ("Top", Math.Abs(actual.Top - work.Top - 6)), ("Bottom", Math.Abs(work.Bottom - actual.Bottom - 6)) };
        return edges.Where(p => p.Item2 <= threshold).OrderBy(p => p.Item2).Select(p => p.Item1).FirstOrDefault() ?? "Free";
    }
}

internal sealed class SnapGuides : Window
{
    readonly Canvas canvas = new(); readonly IntPtr monitor; readonly double scale;
    internal SnapGuides(Shell shell, IntPtr monitor)
    {
        this.monitor = monitor; scale = Native.Scale(monitor); var work = PluginSnapping.WorkArea(shell, monitor);
        Width = work.Width / scale; Height = work.Height / scale; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowActivated = false; ShowInTaskbar = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent; IsHitTestVisible = false; Content = canvas;
        var local = new Rect(0, 0, Width, Height); var box = PluginSnapping.CenterBox(local);
        var outline = new Rectangle { Width = box.Width, Height = box.Height, Stroke = Theme.Accent, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 5 }, Opacity = .65 }; Canvas.SetLeft(outline, box.Left); Canvas.SetTop(outline, box.Top); canvas.Children.Add(outline);
        foreach (string position in PluginSnapping.Positions.Skip(1).Take(12)) { var target = PluginSnapping.Place(local, new Size(12, 12), position, .5, .5); var dot = new Ellipse { Width = 12, Height = 12, Fill = Theme.Accent, Opacity = .8 }; Canvas.SetLeft(dot, target.Left); Canvas.SetTop(dot, target.Top); canvas.Children.Add(dot); }
        SourceInitialized += (_, _) => { var h = new WindowInteropHelper(this).Handle; long style = Native.GetWindowLong(h, -20).ToInt64(); Native.SetWindowLong(h, -20, new IntPtr(style | 0x080000A0)); Native.SetWindowPos(h, new IntPtr(-1), (int)work.Left, (int)work.Top, (int)work.Width, (int)work.Height, 0x50); };
    }
}
