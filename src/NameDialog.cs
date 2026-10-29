using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;

namespace Harbor;

internal static class NameDialog
{
    internal static string? Ask(Window owner, string title, string initial)
    {
        var window = new Window { Owner = owner, Title = title, Width = 440, Height = 205, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = Theme.Panel };
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(Theme.Label(title, 22));
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 18, 0, 14) }; body.Children.Add(box);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(Theme.Button("Cancel", "Cancel", () => window.DialogResult = false));
        var save = Theme.Button("Save", "Save name", () => { string value = box.Text.Trim(); if (value is "." or ".." || value.Length == 0 || value.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || value.EndsWith('.') || value.EndsWith(' ')) return; window.DialogResult = true; }); save.IsDefault = true; buttons.Children.Add(save); body.Children.Add(buttons); window.Content = Theme.Card(body); window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return window.ShowDialog() == true ? box.Text.Trim() : null;
    }
    internal static bool Confirm(Window owner, string title, string message)
    {
        var window = new Window { Owner = owner, Title = title, Width = 450, Height = 210, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = Theme.Panel };
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(Theme.Label(title, 22)); body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 14) }); var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; row.Children.Add(Theme.Button("Cancel", "Cancel", () => window.DialogResult = false)); row.Children.Add(Theme.Button("Continue", "Confirm " + title, () => window.DialogResult = true)); body.Children.Add(row); window.Content = Theme.Card(body); return window.ShowDialog() == true;
    }
}
