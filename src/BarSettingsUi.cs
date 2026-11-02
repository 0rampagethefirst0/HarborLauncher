using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Harbor;

internal static class BarSettingsUi
{
    internal static void Tabs(Panel host, string[] names, string selected, Action<string> choose)
    {
        host.Children.Clear();
        foreach (string name in names) { var button = Theme.Button(name, name + " settings", () => choose(name)); button.Background = name == selected ? Theme.Raised : System.Windows.Media.Brushes.Transparent; button.Foreground = name == selected ? Theme.Accent : Theme.Muted; button.Padding = new Thickness(12, 8, 12, 8); button.Margin = new Thickness(0, 0, 5, 0); host.Children.Add(button); }
    }
    internal static StackPanel Section(Panel host, string title)
    {
        var body = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };
        var label = Theme.Label(title, 12, Theme.Muted); label.Margin = new Thickness(0, 0, 0, 6); body.Children.Add(label);
        var card = Theme.Card(body, 12); card.Margin = new Thickness(0, 0, 0, 10); host.Children.Add(card); return body;
    }
    internal static void Note(Panel host, string text) => host.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12, Margin = new Thickness(2, 0, 2, 10), LineHeight = 18 });
    internal static void Toggle(Panel host, string title, bool initial, Action<bool> set)
    {
        var box = new CheckBox { Content = title, Style = (Style)Application.Current.FindResource("Harbor.Switch"), IsChecked = initial, FontSize = 13, Margin = new Thickness(0, 6, 0, 6) };
        AutomationProperties.SetName(box, title); box.Checked += (_, _) => set(true); box.Unchecked += (_, _) => set(false); host.Children.Add(box);
    }
    internal static Slider Slider(Panel host, string title, double min, double max, double initial, string unit, Action<double> set, double step = 1)
    {
        initial = Math.Clamp(initial, min, max); var row = new DockPanel { Margin = new Thickness(0, 7, 0, 0) }; var value = Theme.Label(initial.ToString("0.#") + " " + unit, 12, Theme.Accent); DockPanel.SetDock(value, System.Windows.Controls.Dock.Right); row.Children.Add(value); row.Children.Add(Theme.Label(title, 13)); host.Children.Add(row);
        var slider = new Slider { Minimum = min, Maximum = max, Value = initial, TickFrequency = step, SmallChange = step, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 5) };
        AutomationProperties.SetName(slider, title); slider.ValueChanged += (_, _) => { set(slider.Value); value.Text = slider.Value.ToString("0.#") + " " + unit; }; host.Children.Add(slider); return slider;
    }
    internal static void Advanced(Panel host, string title, Action<Panel> build)
    {
        var body = new StackPanel { Margin = new Thickness(2, 8, 2, 4) }; build(body);
        var expander = new Expander { Header = title, Content = body, Foreground = Theme.Muted, FontSize = 13, Margin = new Thickness(2, 3, 2, 10) }; expander.Expanded += (_, _) => Motion.Enter(body, 0); host.Children.Add(expander);
    }
}
