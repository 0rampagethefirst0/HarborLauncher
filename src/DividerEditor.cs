using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Harbor;

internal sealed class DividerVisual : Border
{
    readonly Border line = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly FrameworkElement grip = UiIcons.Icon("grip", 12);
    bool resize; Point origin; double start;
    internal DividerVisual(Action<double> resizeWidth, Action commit)
    {
        Background = Brushes.Transparent; Cursor = Cursors.SizeWE; CornerRadius = new CornerRadius(5);
        var grid = new Grid(); grid.Children.Add(line); grip.Opacity = 0; grip.HorizontalAlignment = HorizontalAlignment.Center; grip.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(grip); Child = grid;
        ToolTip = "Drag to move · Alt + drag to resize spacing · right-click for divider settings";
        MouseEnter += (_, _) => { Background = Theme.Raised; grip.Opacity = .9; line.Opacity = .25; };
        MouseLeave += (_, _) => { if (resize) return; Background = Brushes.Transparent; grip.Opacity = 0; line.Opacity = 1; };
        PreviewMouseLeftButtonDown += (_, e) => { if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return; resize = true; origin = e.GetPosition(this); start = ActualWidth; CaptureMouse(); e.Handled = true; };
        PreviewMouseMove += (_, e) => { if (!resize || e.LeftButton != MouseButtonState.Pressed) return; Width = Math.Clamp(start + e.GetPosition(this).X - origin.X, 20, 160); resizeWidth(Width); e.Handled = true; };
        PreviewMouseLeftButtonUp += (_, e) => { if (!resize) return; resize = false; ReleaseMouseCapture(); commit(); e.Handled = true; };
    }
    internal void Configure(double width, double thickness, double length, bool space, double availableHeight)
    {
        Width = Math.Clamp(width <= 0 ? 24 : width, 20, 160); Height = Math.Max(20, availableHeight); line.Width = Math.Clamp(thickness, 1, 6); line.Height = Math.Clamp(length, 6, Math.Max(6, availableHeight - 4)); line.Background = space ? Brushes.Transparent : Theme.Line;
    }
}

internal sealed class DividerEditor : Window
{
    internal DividerEditor(Shell shell, string title, Func<double> width, Action<double> setWidth, Func<double> thickness, Action<double> setThickness, Func<double> length, Action<double> setLength, Func<bool> space, Action<bool> setSpace, Action<int> move, Action changed)
    {
        Title = title; Theme.PopupWindow(this, 370, 390, shell.CurrentMonitor(), false); Topmost = true;
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(Theme.Label(title, 24)); body.Children.Add(new TextBlock { Text = "Drag the larger grip to move it. Hold Alt while dragging to change its spacing.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(0, 8, 0, 10) });
        var toggle = new CheckBox { Content = "Space without a line", IsChecked = space(), Margin = new Thickness(0, 7, 0, 7) }; toggle.Checked += (_, _) => { setSpace(true); changed(); }; toggle.Unchecked += (_, _) => { setSpace(false); changed(); }; body.Children.Add(toggle);
        Slider("Spacing", 20, 160, width(), setWidth); Slider("Line thickness", 1, 6, thickness(), setThickness); Slider("Line length", 6, 64, length(), setLength);
        var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) }; actions.Children.Add(Theme.Button("← Move left", "Move divider left", () => { move(-1); changed(); })); actions.Children.Add(Theme.Button("Move right →", "Move divider right", () => { move(1); changed(); })); actions.Children.Add(Theme.Button("Done", "Close divider settings", Close)); body.Children.Add(actions); Content = Theme.Card(body, 16);
        void Slider(string label, double min, double max, double initial, Action<double> set) { var value = Theme.Label(label + " · " + initial.ToString("0") + " px", 13); body.Children.Add(value); var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(initial <= 0 ? min : initial, min, max), IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(0, 5, 0, 10) }; slider.ValueChanged += (_, _) => { set(slider.Value); value.Text = label + " · " + slider.Value.ToString("0") + " px"; changed(); }; body.Children.Add(slider); }
    }
}
