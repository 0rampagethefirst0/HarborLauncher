using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class PreviewVerification
{
    internal static void Run(Shell shell, Action<string, bool, object?> check)
    {
        var mods = shell.Mods.Catalog.Where(m => m.Id.StartsWith("harbor.")).ToList();
        int count = 0;
        foreach (var mod in mods)
            foreach (string suffix in new[] { ".png", "-film.png" }) {
                var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Harbor;component/Previews/" + mod.Id + suffix));
                if (resource == null) continue;
                using (resource.Stream) {
                    var decoder = BitmapDecoder.Create(resource.Stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None); var frame = decoder.Frames[0];
                    bool valid = suffix == ".png" ? frame.PixelWidth == ModDemoScene.Width && frame.PixelHeight == ModDemoScene.Height : frame.PixelWidth == ModDemoScene.Width * ModDemoScene.Columns && frame.PixelHeight == ModDemoScene.Height * (ModDemoScene.Frames / ModDemoScene.Columns);
                    if (valid) count++;
                }
            }
        check("All 36 presets ship a correctly sized poster and 24-frame demonstration", count == 72, new { Assets = count });
        string before = File.ReadAllText(Path.Combine(Settings.DataDir, "settings.json")); int notices = shell.Notifications.Items.Count;
        ModDemoScene Scene(string component) => new(shell, mods.First(m => m.Component == component));
        var calculator = Scene("calculator"); calculator.Draw(.72); ModDemoRecorder.Snapshot(calculator.View);
        check("Calculator preview uses its real expression evaluator", Elements(calculator.View).OfType<TextBox>().Any(t => t.Text == "(12 + 8) * 4") && Elements(calculator.View).OfType<TextBlock>().Any(t => t.Text == "80"), null);
        var converter = Scene("converter"); converter.Draw(.72); ModDemoRecorder.Snapshot(converter.View);
        check("Converter preview uses the actual unit conversion controls", Elements(converter.View).OfType<ComboBox>().Any(c => c.SelectedIndex == 8) && Elements(converter.View).OfType<TextBlock>().Any(t => t.Text == "62.137119"), null);
        var checklist = Scene("tasks"); checklist.Draw(0); checklist.Draw(.9);
        check("Checklist demonstration changes real checkboxes", Elements(checklist.View).OfType<CheckBox>().Any() && Elements(checklist.View).OfType<CheckBox>().All(c => c.IsChecked == true), null);
        var note = Scene("notes"); note.Draw(.72);
        check("Notes demonstration types into the actual editor", Elements(note.View).OfType<TextBox>().Any(t => t.Text.Contains("Finish the icon set")), null);
        var topmost = Scene("window-topmost"); var volume = Scene("dock-wheel-volume"); var monitor = Scene("dock-monitor-only");
        check("Behavior previews have individual scenes and instructions", topmost.Renderer != volume.Renderer && monitor.Renderer != volume.Renderer && volume.Instruction.Contains("70%") && monitor.Instruction.Contains("own display"), null);
        check("Preview input never saves user fields or adds notifications", File.ReadAllText(Path.Combine(Settings.DataDir, "settings.json")) == before && shell.Notifications.Items.Count == notices, null);
    }
    static IEnumerable<DependencyObject> Elements(DependencyObject node)
    {
        yield return node; foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var e in Elements(child)) yield return e;
    }
}
