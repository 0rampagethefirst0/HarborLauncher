using System;
using System.Windows;
using System.Windows.Controls;

namespace Harbor;

internal sealed class ModPreviewWindow : Window
{
    internal ModPreviewWindow(Shell shell, ModDefinition mod)
    {
        Title = "Harbor — " + mod.Name + " preview"; Theme.AppWindow(this, 1080, 760); MinWidth = 720; MinHeight = 520;
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, mod.Name + " · preview"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var words = new StackPanel { Margin = new Thickness(24, 16, 24, 20) }; words.Children.Add(Theme.Label(mod.Name, 26)); words.Children.Add(new TextBlock { Text = mod.Description, FontSize = 15, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) }); words.Children.Add(new TextBlock { Text = string.Join("\n", ModHelp.Steps(mod)), FontSize = 14, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), LineHeight = 23 }); DockPanel.SetDock(words, System.Windows.Controls.Dock.Bottom); root.Children.Add(words);
        root.Children.Add(new ModBanner(mod, shell, 430, true) { Height = double.NaN, Margin = new Thickness(24, 18, 24, 0) }); Content = root;
    }
}
