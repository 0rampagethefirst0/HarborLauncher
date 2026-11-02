using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Harbor;

internal sealed class PluginReadingEditor : Window
{
    internal PluginReadingEditor(Shell shell, ModDefinition mod)
    {
        Title = mod.Name + " — compact readings"; Theme.PopupWindow(this, 430, 480, shell.CurrentMonitor(), false); Topmost = true;
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(Theme.Label(mod.Name, 23));
        var editors = new Dictionary<string, Func<string>>(); string key = PluginPresentation.Key(mod);
        if (key is "system" or "resource-desk") Choice("meter-mode", "Meter mode", new[] { "CPU + RAM", "CPU", "RAM" });
        if (key == "storage") { Text("drives", "Drive letters, separated by commas", "C:,D:,E:"); Choice("drive-mode", "Drive display", new[] { "All drives", "First drive", "Cycle drives", "Used percent" }); }
        if (key is "clock" or "world-clock") { Text("format", "Time format", "HH:mm · h:mm tt · HH:mm:ss"); if (key == "clock") Text("dateFormat", "Expanded date format", "dddd, MMMM d"); }
        Text("compact-format", "Custom reading (optional)", "CPU {cpu:0}% · RAM {ram:0}% · {time:HH:mm}");
        var footer = new WrapPanel { Margin = new Thickness(0, 22, 0, 0) }; footer.Children.Add(Theme.Button("Cancel", "Cancel", Close)); footer.Children.Add(Theme.Button("Apply", "Save compact readings", () => { shell.Mods.Configure(mod, editors.ToDictionary(p => p.Key, p => p.Value())); shell.RebuildMods(); Close(); })); body.Children.Add(footer);
        Content = Theme.Card(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 16);
        void Text(string option, string label, string hint) { body.Children.Add(Theme.Label(label, 13)); var box = new TextBox { Text = shell.Mods.Value(mod, option), MinHeight = 34, Margin = new Thickness(0, 6, 0, 4) }; body.Children.Add(box); body.Children.Add(Theme.Label(hint, 11, Theme.Muted)); editors[option] = () => box.Text; }
        void Choice(string option, string label, string[] choices) { body.Children.Add(Theme.Label(label, 13)); var box = new ComboBox { ItemsSource = choices, SelectedItem = shell.Mods.Value(mod, option, choices[0]), MinHeight = 34, Margin = new Thickness(0, 6, 0, 14) }; body.Children.Add(box); editors[option] = () => box.SelectedItem as string ?? choices[0]; }
    }
}
