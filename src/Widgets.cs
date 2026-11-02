using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Harbor;

internal sealed class TimerState
{
    internal readonly Stopwatch Watch = new();
    internal double Seconds = 1500;
    internal bool Finished;
}
internal sealed class Widget
{
    readonly Shell shell;
    readonly ModDefinition mod;
    readonly StackPanel body = new();
    readonly List<Action> ticks = new();
    readonly List<Action> flush = new();
    readonly double width;
    readonly bool preview;
    readonly WidgetDemoData? demo;
    internal Border View { get; }
    internal Thumb Handle { get; }
    internal Widget(Shell shell, ModDefinition mod, bool preview = false, WidgetDemoData? demo = null, bool inline = false)
    {
        this.shell = shell; this.mod = mod; this.preview = preview; this.demo = demo; width = shell.Mods.Number(mod, "width", 280, 180, 650);
        body.Margin = new Thickness(16, 0, 16, 16);
        body.IsHitTestVisible = !preview;
        var root = new DockPanel();
        var header = new DockPanel { Margin = new Thickness(12, 8, 8, 4) };
        var settings = Theme.IconButton("\uE713", "Customize " + mod.Name, () => shell.Plugins.Customize(mod, shell.CurrentMonitor())); settings.Padding = new Thickness(6); DockPanel.SetDock(settings, System.Windows.Controls.Dock.Right); header.Children.Add(settings);
        Handle = new Thumb { Height = 25, Cursor = System.Windows.Input.Cursors.SizeAll, Template = new ControlTemplate(typeof(Thumb)) { VisualTree = Header() } }; header.Children.Add(Handle); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); if (!inline) root.Children.Add(header); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        View = Theme.Card(root); View.Width = width; View.Tag = mod;
        if (!inline && !preview) { View.ContextMenu = new ContextMenu(); View.ContextMenuOpening += (_, e) => { if (e.Source is System.Windows.Controls.Primitives.TextBoxBase) return; e.Handled = true; shell.Plugins.Customize(mod, shell.CurrentMonitor()); }; }
        if (inline) { View.Background = Brushes.Transparent; View.BorderThickness = new Thickness(0); body.Margin = new Thickness(12); }
        View.CornerRadius = new CornerRadius(shell.Mods.Number(mod, "radius", 14, 0, 60)); View.Opacity = shell.Mods.Number(mod, "opacity", 1, .005, 1);
        bool CustomColor(string key) => Value(key).Length > 0 && (!mod.Id.StartsWith("harbor.") || Value(key) != mod.Options.FirstOrDefault(o => o.Key == key)?.Default);
        try { if (CustomColor("background")) View.Background = Theme.Brush(Value("background")); if (CustomColor("border")) View.BorderBrush = Theme.Brush(Value("border")); } catch { }
        switch (mod.Component)
        {
            case "dashboard": if (mod.Layout != null) { var layout = new WidgetLayout(shell, mod, preview, demo); body.Children.Add(layout.Build(mod.Layout, width - 32)); ticks.Add(layout.Tick); } break;
            case "clock": Clock(false); break;
            case "world-clock": Clock(true); break;
            case "system": SystemMonitor(); break;
            case "network": Network(); break;
            case "storage": Storage(); break;
            case "uptime": { var label = Big(); ticks.Add(() => label.Text = (demo == null ? TimeSpan.FromMilliseconds(Environment.TickCount64) : TimeSpan.FromHours(52).Add(TimeSpan.FromSeconds(demo.Phase * 5))).ToString(@"d\d\ hh\:mm\:ss")); break; }
            case "notes": Notes(); break;
            case "tasks": Tasks(); break;
            case "focus": Timer(true); break;
            case "stopwatch": Timer(false); break;
            case "calculator": Calculator(); break;
            case "converter": Converter(); break;
            case "folders": Folders(); break;
            case "clipboard": ClipboardShelf(); break;
            case "layouts": foreach (string layout in new[] { "columns", "grid", "stack" }) body.Children.Add(Theme.Button(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(layout), layout + " layout", () => shell.Arrange(shell.CurrentMonitor(), layout))); body.Children.Add(Theme.Button("Undo", "Undo window layout", () => shell.UndoLayout(shell.CurrentMonitor()))); break;
            case "launchpad": Launchpad(); break;
        }
        Tick();
        FrameworkElementFactory Header() { var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetValue(TextBlock.TextProperty, mod.Name); label.SetValue(TextBlock.ForegroundProperty, Theme.Muted); label.SetValue(TextBlock.FontSizeProperty, 11.0); label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center); return label; }
    }
    internal void Tick() { foreach (var tick in ticks) try { tick(); } catch (Exception ex) { Log.Write("Widget " + mod.Id + ": " + ex.Message); } }
    internal void Flush() { foreach (var save in flush) save(); }
    string Value(string key, string fallback = "") => shell.Mods.Value(mod, key, fallback);
    TextBlock Big() { var label = Theme.Label("", 28); body.Children.Add(label); return label; }
    void Clock(bool world)
    {
        var clock = Big(); clock.FontSize = world ? 30 : 46; var date = Theme.Label("", 12, Theme.Muted); body.Children.Add(date);
        ticks.Add(() =>
        {
            DateTime now = demo == null ? DateTime.Now : TimeZoneInfo.ConvertTime(demo.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).DateTime;
            if (world) { try { now = TimeZoneInfo.ConvertTime(demo?.Now ?? DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(Value("zone", "Tokyo Standard Time"))).DateTime; } catch { date.Text = "Choose a Windows time zone in mod settings"; return; } }
            try { clock.Text = now.ToString(Value("format", "HH:mm")); date.Text = world ? Value("zone") : now.ToString(Value("dateFormat", "dddd, MMMM d")); } catch (FormatException) { clock.Text = now.ToString("HH:mm"); date.Text = "Check the clock format in mod settings"; }
        });
    }
    void SystemMonitor()
    {
        var text = Theme.Label("", 15); body.Children.Add(text);
        var line = new Polyline { Height = 40, Width = width - 34, Stroke = Theme.Accent, StrokeThickness = 1.5, Margin = new Thickness(0, 12, 0, 0) }; body.Children.Add(line); var history = new Queue<double>();
        if (demo != null) for (int i = 0; i < 24; i++) history.Enqueue(26 + 13 * Math.Sin(i * .65));
        ticks.Add(() => { text.Text = $"CPU  {demo?.Cpu ?? shell.CpuUsage:0}%     RAM  {demo?.Ram ?? shell.MemoryUsage}%"; history.Enqueue(demo?.Cpu ?? shell.CpuUsage); int limit = (int)shell.Mods.Number(mod, "history", 60, 10, 300); while (history.Count > limit) history.Dequeue(); var points = new PointCollection(); int i = 0; foreach (double cpu in history) points.Add(new Point(i++ * (width - 34) / Math.Max(1, history.Count - 1), 40 * (1 - cpu / 100))); line.Points = points; });
    }
    void Network()
    {
        var down = Theme.Label("", 20); var up = Theme.Label("", 13, Theme.Muted); body.Children.Add(down); body.Children.Add(up);
        if (demo != null) { ticks.Add(() => { down.Text = "↓ " + FileTools.Size((long)(1_200_000 + demo.Phase * 11_200_000)) + "/s"; up.Text = "↑ " + FileTools.Size((long)(380_000 + demo.Phase * 1_400_000)) + "/s"; }); return; }
        long oldDown = 0, oldUp = 0; var clock = Stopwatch.StartNew(); double previous = 0;
        ticks.Add(() => { long received = 0, sent = 0; foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)) { var stats = adapter.GetIPStatistics(); received += stats.BytesReceived; sent += stats.BytesSent; } double now = clock.Elapsed.TotalSeconds, seconds = Math.Max(.1, now - previous); down.Text = "↓ " + FileTools.Size(oldDown == 0 ? 0 : Math.Max(0, (long)((received - oldDown) / seconds))) + "/s"; up.Text = "↑ " + FileTools.Size(oldUp == 0 ? 0 : Math.Max(0, (long)((sent - oldUp) / seconds))) + "/s"; oldDown = received; oldUp = sent; previous = now; });
    }
    void Storage()
    {
        var label = Theme.Label("", 13); label.TextWrapping = TextWrapping.Wrap; body.Children.Add(label); DateTime next = DateTime.MinValue;
        if (demo != null) { label.Text = "C:\\  642 GB free of 1.00 TB\nD:\\  1.20 TB free of 2.00 TB"; return; }
        ticks.Add(() => { if (DateTime.UtcNow < next) return; next = DateTime.UtcNow.AddSeconds(30); label.Text = string.Join("\n", Value("drives", "C:").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(letter => { try { var drive = new DriveInfo(letter); return drive.IsReady ? $"{drive.Name}  {FileTools.Size(drive.AvailableFreeSpace)} free of {FileTools.Size(drive.TotalSize)}" : letter + " unavailable"; } catch { return letter + " unavailable"; } })); });
    }
    void Notes()
    {
        var box = new TextBox { Text = Value("content"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 400, MaxLength = 20000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 14, ToolTip = Value("placeholder", "Quick notes") };
        if (demo != null) ticks.Add(() => box.Text = demo.Notes);
        bool dirty = false;
        var save = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        void Commit() { save.Stop(); if (!preview && dirty) { shell.Mods.Set(mod, "content", box.Text, false); dirty = false; } }
        save.Tick += (_, _) => Commit();
        box.TextChanged += (_, _) => { if (preview) return; dirty = true; save.Stop(); save.Start(); }; ViewUnloaded(Commit); body.Children.Add(box);
    }
    void Tasks()
    {
        int index = 0;
        foreach (string task in Value("items").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string key = "checked-" + index++; var check = new CheckBox { Content = task.Trim(), IsChecked = Value(key) == "true", FontSize = 13, Margin = new Thickness(0, 5, 0, 5) };
            check.Checked += (_, _) => { if (!preview) shell.Mods.Set(mod, key, "true", false); }; check.Unchecked += (_, _) => { if (!preview) shell.Mods.Set(mod, key, "false", false); }; body.Children.Add(check);
            if (demo != null) { int taskIndex = index; ticks.Add(() => check.IsChecked = demo.Phase > taskIndex * .24); }
        }
    }
    void Timer(bool focus)
    {
        object saved; if (preview) saved = new TimerState { Seconds = shell.Mods.Number(mod, "minutes", 25, .1, 360) * 60 }; else if (!shell.ModRuntime.TryGetValue(mod.Id, out saved!)) shell.ModRuntime[mod.Id] = saved = new TimerState { Seconds = shell.Mods.Number(mod, "minutes", 25, .1, 360) * 60 };
        var state = (TimerState)saved; var text = Big(); var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (focus && !state.Watch.IsRunning && state.Watch.ElapsedTicks == 0) state.Seconds = shell.Mods.Number(mod, "minutes", 25, .1, 360) * 60;
        var start = Theme.Button("Start / pause", "Start or pause timer", () => { if (state.Watch.IsRunning) state.Watch.Stop(); else { if (focus && state.Finished) { state.Watch.Reset(); state.Seconds = shell.Mods.Number(mod, "minutes", 25, .1, 360) * 60; state.Finished = false; } state.Watch.Start(); } });
        row.Children.Add(start); row.Children.Add(Theme.Button("Reset", "Reset timer", () => { state.Watch.Reset(); state.Finished = false; state.Seconds = shell.Mods.Number(mod, "minutes", 25, .1, 360) * 60; })); body.Children.Add(row);
        ticks.Add(() => { double elapsed = demo?.Elapsed ?? state.Watch.Elapsed.TotalSeconds; double seconds = focus ? Math.Max(0, state.Seconds - elapsed) : elapsed; text.Text = TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss"); if (focus && seconds <= 0 && !state.Finished) { state.Finished = true; state.Watch.Stop(); if (!preview) shell.Notifications.Add("Focus timer finished", "Your focus session is complete."); } });
    }
    void Calculator()
    {
        var input = new TextBox { ToolTip = "Arithmetic expression", MaxLength = 150 }; var answer = Theme.Label("", 22); answer.Margin = new Thickness(0, 10, 0, 0); body.Children.Add(input); body.Children.Add(answer);
        input.TextChanged += (_, _) => { try { if (!Regex.IsMatch(input.Text, @"^[\d\s.+*/()%\-]+$")) { answer.Text = ""; return; } using var table = new DataTable { Locale = CultureInfo.InvariantCulture }; answer.Text = Convert.ToString(table.Compute(input.Text, ""), CultureInfo.CurrentCulture) ?? ""; } catch { answer.Text = "…"; } };
        if (demo != null) ticks.Add(() => { const string expression = "(12 + 8) * 4"; input.Text = expression[..Math.Clamp((int)(demo.Phase * 22), 0, expression.Length)]; });
    }
    void Converter()
    {
        var input = new TextBox { Text = "1" }; var modes = new ComboBox { ItemsSource = new[] { "°C → °F", "°F → °C", "m → ft", "ft → m", "km → mi", "mi → km", "kg → lb", "lb → kg", "km/h → mph", "mph → km/h", "MB → MiB", "MiB → MB" }, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 8) }; var output = Theme.Label("", 22);
        void ConvertValue() { if (!double.TryParse(input.Text, out double value)) { output.Text = ""; return; } double result = modes.SelectedIndex switch { 0 => value * 1.8 + 32, 1 => (value - 32) / 1.8, 2 => value * 3.280839895, 3 => value / 3.280839895, 4 => value / 1.609344, 5 => value * 1.609344, 6 => value * 2.204622622, 7 => value / 2.204622622, 8 => value / 1.609344, 9 => value * 1.609344, 10 => value / 1.048576, _ => value * 1.048576 }; output.Text = result.ToString("0.######"); }
        input.TextChanged += (_, _) => ConvertValue(); modes.SelectionChanged += (_, _) => ConvertValue(); body.Children.Add(input); body.Children.Add(modes); body.Children.Add(output); ConvertValue();
        if (demo != null) ticks.Add(() => { modes.SelectedIndex = demo.Phase < .3 ? 0 : 8; input.Text = demo.Phase < .3 ? "20" : "100"; });
    }
    void Folders()
    {
        foreach (string path in Value("paths").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) { string expanded = Environment.ExpandEnvironmentVariables(path); var button = Theme.Button(System.IO.Path.GetFileName(expanded.TrimEnd('\\')), "Open " + expanded, () => shell.OpenFiles(expanded)); button.HorizontalContentAlignment = HorizontalAlignment.Left; body.Children.Add(button); }
    }
    void ClipboardShelf()
    {
        if (demo != null) { var samples = new StackPanel(); body.Children.Add(samples); ticks.Add(() => { samples.Children.Clear(); foreach (string value in demo.Clipboard) { var b = Theme.Button(value, "Copy text back to clipboard", () => { }); b.HorizontalContentAlignment = HorizontalAlignment.Left; samples.Children.Add(b); } }); return; }
        if (!shell.ModRuntime.TryGetValue(mod.Id, out var history)) shell.ModRuntime[mod.Id] = history = new List<string>(); var values = (List<string>)history;
        var list = new StackPanel(); body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); uint previous = Native.GetClipboardSequenceNumber();
        void Refresh() { list.Children.Clear(); foreach (string value in values) { var item = Theme.Button(value.Length > 60 ? value[..60] + "…" : value, "Copy text back to clipboard", () => { try { Clipboard.SetText(value); } catch { } }); item.HorizontalContentAlignment = HorizontalAlignment.Left; list.Children.Add(item); } if (values.Count == 0) list.Children.Add(Theme.Label("Copy text to add it here.", 12, Theme.Muted)); }
        ticks.Add(() => { uint sequence = Native.GetClipboardSequenceNumber(); if (sequence == previous) return; previous = sequence; try { if (!Clipboard.ContainsText()) return; string text = Clipboard.GetText(); if (text.Length > 5000 || text.Length == 0) return; values.Remove(text); values.Insert(0, text); int limit = (int)shell.Mods.Number(mod, "limit", 12, 1, 50); if (values.Count > limit) values.RemoveRange(limit, values.Count - limit); Refresh(); } catch { } }); Refresh();
    }
    void Launchpad()
    {
        foreach (string line in Value("apps").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) { string[] parts = line.Split('|', 2, StringSplitOptions.TrimEntries); if (parts.Length != 2) continue; var button = Theme.Button(parts[0], "Open " + parts[0], () => shell.Launch(Environment.ExpandEnvironmentVariables(parts[1]))); button.HorizontalContentAlignment = HorizontalAlignment.Left; body.Children.Add(button); }
    }
    void ViewUnloaded(Action action) { flush.Add(action); body.Unloaded += (_, _) => action(); }
}
