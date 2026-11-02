using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Harbor;

internal sealed class TopDockControls : Window
{
    readonly Shell shell;
    readonly StackPanel body = new(), tabs = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
    Panel inputs;
    readonly ScrollViewer scroll;
    readonly TextBlock status = Theme.Label("Changes save automatically", 11, Theme.Muted);
    readonly DispatcherTimer save = new() { Interval = TimeSpan.FromMilliseconds(250) };
    string page = "Buttons";
    bool dirty, dragging;
    internal TopDockControls(Shell shell, IntPtr monitor)
    {
        this.shell = shell; inputs = body; Title = "Harbor — Dock controls"; Theme.PopupWindow(this, 480, 650, monitor, false);
        var root = new DockPanel { Margin = new Thickness(19) }; var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var close = Theme.Button(UiIcons.Icon("close"), "Close Dock controls", Close); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close); header.Children.Add(Theme.Label("Dock controls", 24)); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        DockPanel.SetDock(tabs, System.Windows.Controls.Dock.Top); root.Children.Add(tabs);
        DockPanel.SetDock(status, System.Windows.Controls.Dock.Bottom); status.Margin = new Thickness(0, 10, 0, 0); root.Children.Add(status); scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Motion.SmoothScroll(scroll); root.Children.Add(scroll);
        var card = Theme.Card(root, 17); var offset = new TranslateTransform(); card.RenderTransform = offset; Content = card;
        SourceInitialized += (_, _) => { double scale = Native.Scale(monitor); var bounds = Native.Monitor(monitor).Monitor; int width = (int)(Width * scale), height = (int)(Height * scale); Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.Left + (bounds.Width - width) / 2, bounds.Top + (int)((shell.Config.TopBarHeight + 8) * scale), width, height, 0x50); };
        Loaded += (_, _) => { int ms = shell.Config.DockMotionMilliseconds; if (ms > 0 && Settings.TestDataDir == null) offset.BeginAnimation(TranslateTransform.YProperty, Motion.Track(new DoubleAnimation(-22, 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } })); };
        save.Tick += (_, _) => Commit(); Deactivated += (_, _) => { if (!dragging) Close(); }; Closed += (_, _) => { save.Stop(); Commit(); };
        Render();
    }
    void Render(bool keep = true)
    {
        using var viewport = ScrollPosition.Keep(scroll, keep);
        BarSettingsUi.Tabs(tabs, new[] { "Layout", "Style", "Behavior" }, page == "Buttons" ? "Layout" : page, next => { page = next == "Layout" ? "Buttons" : next; Render(false); });
        body.Children.Clear(); var c = shell.Config;
        if (page == "Buttons")
        {
            var add = Theme.Button(UiIcons.Label("Add", 13, "plus"), "Add a Dock item", () => { }); var addMenu = new ContextMenu();
            addMenu.Items.Add(Theme.Item("Button or tool", () => new DockPieceEditor(shell).Show())); addMenu.Items.Add(Theme.Item("Divider", () => { BarLayout.AddDivider(c, "right"); Changed(); Render(); })); addMenu.Items.Add(Theme.Item("Flexible space", () => { BarLayout.AddDivider(c, "right", space: true); Changed(); Render(); })); addMenu.Items.Add(new Separator()); addMenu.Items.Add(Theme.Item("Reset layout", () => { c.TopDockItems = BarLayout.Defaults(c); Changed(); Render(); })); add.Click += (_, _) => { addMenu.PlacementTarget = add; addMenu.IsOpen = true; }; add.HorizontalAlignment = HorizontalAlignment.Right; add.Margin = new Thickness(0, 0, 0, 10); body.Children.Add(add);
            foreach (var item in c.TopDockItems.ToArray())
            {
                var row = new DockPanel { Background = Theme.Panel, Margin = new Thickness(0, 0, 0, 5), MinHeight = 38, AllowDrop = true };
                var grip = Theme.Button(UiIcons.Icon("grip", 17), "Drag " + BarLayout.Label(item), () => { }); grip.Width = 24; grip.Padding = new Thickness(2); grip.Cursor = Cursors.SizeAll; row.Children.Add(grip);
                var edit = Theme.Button(UiIcons.Icon("controls", 13), "Options for " + BarLayout.Label(item), () => { }); edit.Padding = new Thickness(6); DockPanel.SetDock(edit, System.Windows.Controls.Dock.Right); row.Children.Add(edit); var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Edit " + BarLayout.Label(item), () => new DockPieceEditor(shell, item).Show())); var align = new MenuItem { Header = "Align" }; foreach (string group in new[] { "left", "center", "right" }) { var choice = Theme.Item(char.ToUpperInvariant(group[0]) + group[1..], () => { BarLayout.Move(c, item.Id, null, group); Changed(); Render(); }); choice.IsCheckable = true; choice.IsChecked = item.Group == group; align.Items.Add(choice); } menu.Items.Add(align); if (item.Id.StartsWith("divider.")) { menu.Items.Add(new Separator()); menu.Items.Add(Theme.Item("Remove", () => { c.TopDockItems.Remove(item); Changed(); Render(); })); } edit.Click += (_, _) => { menu.PlacementTarget = edit; menu.IsOpen = true; }; row.ContextMenu = menu;
                var toggle = new CheckBox { Style = (Style)FindResource("Harbor.Switch"), Content = BarLayout.Label(item), IsChecked = item.Enabled && BarLayout.Legacy(c, BarLayout.Component(item)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) }; toggle.Checked += (_, _) => { BarLayout.Enable(c, item, true); Changed(); }; toggle.Unchecked += (_, _) => { BarLayout.Enable(c, item, false); Changed(); }; AutomationProperties.SetName(toggle, "Show " + BarLayout.Label(item)); row.Children.Add(toggle);
                Point start = default; bool armed = false; grip.PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(grip); armed = true; };
                grip.PreviewMouseLeftButtonUp += (_, _) => armed = false;
                grip.PreviewMouseMove += (_, e) => { if (!armed || e.LeftButton != MouseButtonState.Pressed || dragging || (e.GetPosition(grip) - start).Length < 5) return; armed = false; dragging = true; grip.ReleaseMouseCapture(); try { DragDrop.DoDragDrop(grip, new DataObject(BarLayout.DragFormat, item.Id), DragDropEffects.Move); } finally { dragging = false; Dispatcher.BeginInvoke(new Action(() => Render())); } };
                row.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(BarLayout.DragFormat) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
                row.Drop += (_, e) => { if (e.Data.GetData(BarLayout.DragFormat) is string id) { BarLayout.Move(c, id, item.Id, item.Group); Changed(); e.Handled = true; } }; body.Children.Add(row);
            }
        }
        else if (page == "Style")
        {
            inputs = BarSettingsUi.Section(body, "Appearance"); Slider("Height", 28, 64, c.TopBarHeight, "px", v => c.TopBarHeight = (int)v); Slider("Icon size", 12, 26, c.TopDockIconSize, "px", v => c.TopDockIconSize = (int)v); Slider("Transparency", .5, 99.5, (1 - c.TopDockOpacity) * 100, "%", v => c.TopDockOpacity = 1 - v / 100, .5); Slider("Rounded corners", 0, 28, c.TopDockRadius, "px", v => c.TopDockRadius = (int)v); inputs = body;
            BarSettingsUi.Advanced(body, "Spacing and text", advanced => { inputs = advanced; Slider("Text size", 10, 18, c.TopDockFontSize, "px", v => c.TopDockFontSize = (int)v); Slider("Button padding", 0, 20, c.TopDockPadding, "px", v => c.TopDockPadding = (int)v); Slider("Button spacing", 0, 18, c.TopDockGap, "px", v => c.TopDockGap = (int)v); Slider("Window title width", 40, 520, c.TopDockTitleWidth, "px", v => c.TopDockTitleWidth = (int)v); Toggle("Bottom border", c.TopDockShowBorder, v => c.TopDockShowBorder = v); Toggle("Harbor name", c.TopDockBrandText, v => c.TopDockBrandText = v); inputs = body; });
            body.Children.Add(Theme.Button("Colors and themes", "Open appearance settings", shell.OpenAppearance));
        }
        else
        {
            inputs = BarSettingsUi.Section(body, "Display");
            Toggle("Show Dock", c.TopDockEnabled && ShellFeatures.On(c, "top-dock"), v => { ShellFeatures.Request(c, "top-dock", v); c.TopDockEnabled = v; });
            Toggle("Only on the main display", c.TopDockPrimaryOnly, v => c.TopDockPrimaryOnly = v);
            Toggle("Keep apps below the Dock", c.TopDockReserveSpace, v => c.TopDockReserveSpace = v);
            inputs = BarSettingsUi.Section(body, "Games");
            Toggle("Hide bars while gaming", c.HideInGames, v => c.HideInGames = v);
            Toggle("Hide bars in full screen", c.HideInFullscreen, v => c.HideInFullscreen = v);
            inputs = body; BarSettingsUi.Advanced(body, "Clock and animation", advanced => { inputs = advanced; Toggle("Stay above windows", c.TopDockOnTop, v => c.TopDockOnTop = v); Toggle("24-hour clock", c.Use24HourClock, v => c.Use24HourClock = v); Toggle("Show seconds", c.TopDockSeconds, v => c.TopDockSeconds = v); Slider("Preview frame rate", 80, 144, c.PreviewFramesPerSecond, "fps", v => c.PreviewFramesPerSecond = (int)v); Slider("Animation frame target", 80, 144, c.AnimationFramesPerSecond, "fps", v => c.AnimationFramesPerSecond = (int)v); Slider("Animation time", 0, 350, c.DockMotionMilliseconds, "ms", v => c.DockMotionMilliseconds = (int)v); inputs = body; });
        }
    }
    void Note(string text) => body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });
    void Toggle(string label, bool initial, Action<bool> change) => BarSettingsUi.Toggle(inputs, label, initial, v => { change(v); Changed(); });
    void Slider(string label, double min, double max, double initial, string unit, Action<double> change, double step = 1) => BarSettingsUi.Slider(inputs, label, min, max, initial, unit, v => { change(v); Changed(); }, step);
    void Changed() { dirty = true; shell.ApplyTopDockPreferences(); save.Stop(); save.Start(); status.Text = "Saving…"; }
    void Commit() { save.Stop(); if (!dirty) return; try { shell.Config.Save(); dirty = false; status.Text = "Saved"; } catch (Exception ex) { status.Text = ex.Message; } }
    internal void PreparePreview(string name) { page = name; Render(); }
}
