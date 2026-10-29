using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Harbor;

internal sealed class ModCreator : Window
{
    readonly Shell shell;
    readonly ModDefinition? original;
    readonly Action<ModDefinition>? saved;
    readonly bool sharing;
    readonly TextBox name = new(), author = new(), version = new() { MaxLength = 40, Text = "1.0.0" }, description = new() { AcceptsReturn = true, MinHeight = 75, TextWrapping = TextWrapping.Wrap, MaxLength = 8000 }, extraTags = new();
    readonly ComboBox main = new() { ItemsSource = ModTaxonomy.Main }, sub = new(), template = new() { DisplayMemberPath = "Name" };
    readonly TextBlock status = Theme.Label("", 12, Theme.Muted), imageStatus = Theme.Label("Native animated preview", 12, Theme.Muted);
    readonly HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);
    string image = "";
    string guideTemplate = "";
    readonly TextBox instructions = new() { AcceptsReturn = true, MinHeight = 130, TextWrapping = TextWrapping.Wrap, MaxLength = 8000 };
    ModDefinition? created;
    internal ModCreator(Shell shell, ModDefinition? original = null, bool upload = false, Action<ModDefinition>? saved = null)
    {
        this.shell = shell; this.original = original; this.saved = saved; sharing = upload; Title = "Harbor — create a plugin"; Theme.AppWindow(this, 820, 850);
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, upload ? "Share a plugin" : "Create a plugin"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var footer = new StackPanel { Margin = new Thickness(22, 12, 22, 18) }; status.TextWrapping = TextWrapping.Wrap; footer.Children.Add(status);
        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(Theme.Button("Save plugin", "Save your plugin locally", () => Attempt(() => { var mod = Save(); status.Text = mod.Name + " saved. Find it in Installed."; })));
        actions.Children.Add(Theme.Button("Design layout", "Open the visual builder", () => Attempt(() => { var mod = Save(); if (mod.Kind != "widget") throw new InvalidOperationException("The visual builder is for widget layouts. Use the plugin’s settings for themes and desktop rules."); new ModDesigner(shell, mod) { Owner = this }.Show(); })));
        actions.Children.Add(Theme.Button("Export package", "Export a shareable plugin", () => Attempt(() => { var mod = Save(); var dialog = new SaveFileDialog { Filter = "Harbor plugin|*.harbormod", FileName = mod.Id + ".harbormod" }; if (dialog.ShowDialog(this) == true) { shell.Mods.ExportConfigured(mod, dialog.FileName); status.Text = "Package exported."; } })));
        var publish = Theme.Button("Upload to community  →", "Prepare your plugin and open the upload page", () => Attempt(() => {
            var mod = Save(); string folder = Path.Combine(Settings.DataDir, "Exports"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, mod.Id + ".json");
            var export = Mods.Read(JsonSerializer.Serialize(mod, Mods.Json)); foreach (var option in export.Options) option.Default = shell.Mods.Value(mod, option.Key);
            AtomicFile.WriteText(path, JsonSerializer.Serialize(export, Mods.Json));
            // The browser owns sign-in; no account credentials are stored by Harbor.
            System.Windows.Clipboard.SetText(path); Community.Open("/upload"); status.Text = "Package ready. Sign in on the community page, choose your JSON file, and paste the copied path into the file picker. Review, then publish.";
        })); publish.Background = Theme.Raised; actions.Children.Add(publish); footer.Children.Add(actions); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var panel = new StackPanel { Margin = new Thickness(24, 10, 24, 10) }; root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        Label(panel, "Start with something useful", 23); Label(panel, "Customize a working capability. Add a category, optional tags and your own banner. Widgets can also be designed in the visual builder.", 13, true);
        Label(panel, "Starting plugin"); template.ItemsSource = (upload ? shell.Mods.Catalog.OrderByDescending(m => m.Id.StartsWith("local.")) : shell.Mods.Catalog.AsEnumerable()).ToList(); template.SelectedItem = original ?? shell.Mods.Catalog.FirstOrDefault(m => upload && m.Id.StartsWith("local.")) ?? shell.Mods.Catalog.First(m => m.Component == "notes"); panel.Children.Add(template);
        Label(panel, "Name"); name.MaxLength = 120; name.Text = original?.Name ?? "My plugin"; panel.Children.Add(name);
        Label(panel, "Creator"); author.MaxLength = 120; author.Text = original?.Id.StartsWith("local.") == true ? original.Author : "Local"; panel.Children.Add(author);
        Label(panel, "Version · increase this for published updates"); if (original?.Id.StartsWith("local.") == true) version.Text = original.Version; panel.Children.Add(version);
        Label(panel, "Description"); description.Text = original?.Description ?? ""; panel.Children.Add(description);
        Label(panel, "How to use · one step per line"); var guideSource = (ModDefinition)template.SelectedItem; instructions.Text = original?.Instructions ?? (string.IsNullOrWhiteSpace(guideSource.Instructions) ? ModHelp.Build(guideSource) : guideSource.Instructions); panel.Children.Add(instructions); AutomationProperties.SetName(instructions, "Plugin usage instructions");
        guideTemplate = instructions.Text;
        var categories = new Grid(); categories.ColumnDefinitions.Add(new ColumnDefinition()); categories.ColumnDefinitions.Add(new ColumnDefinition());
        var mainCol = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; Label(mainCol, "Main category · required"); mainCol.Children.Add(main); categories.Children.Add(mainCol);
        var subCol = new StackPanel(); Label(subCol, "Subcategory · required"); subCol.Children.Add(sub); Grid.SetColumn(subCol, 1); categories.Children.Add(subCol); panel.Children.Add(categories);
        main.SelectionChanged += (_, _) => { string m = main.SelectedItem as string ?? "Tools"; sub.ItemsSource = ModTaxonomy.Children[m]; sub.SelectedIndex = 0; };
        var starter = (ModDefinition)template.SelectedItem; main.SelectedItem = ModTaxonomy.MainOf(starter); sub.SelectedItem = ModTaxonomy.SubOf(starter);
        if (original == null && upload && starter.Id.StartsWith("local.")) { name.Text = starter.Name; author.Text = starter.Author; version.Text = starter.Version; description.Text = starter.Description; foreach (string tag in starter.Tags) tags.Add(tag); }
        image = original?.PreviewImage ?? (upload && starter.Id.StartsWith("local.") ? starter.PreviewImage : ""); if (image.Length > 0) imageStatus.Text = "Custom banner attached";
        template.SelectionChanged += (_, _) => { if (template.SelectedItem is ModDefinition choice) { main.SelectedItem = ModTaxonomy.MainOf(choice); sub.SelectedItem = ModTaxonomy.SubOf(choice); if (instructions.Text == guideTemplate || string.IsNullOrWhiteSpace(instructions.Text)) { guideTemplate = string.IsNullOrWhiteSpace(choice.Instructions) ? ModHelp.Build(choice) : choice.Instructions; instructions.Text = guideTemplate; } } };
        Label(panel, "Banner"); var bannerActions = new WrapPanel(); bannerActions.Children.Add(Theme.Button("Choose image", "Choose a PNG or JPEG banner", PickImage)); bannerActions.Children.Add(Theme.Button("Use native preview", "Use the animated native preview", () => { image = ""; imageStatus.Text = "Native animated preview"; })); imageStatus.Margin = new Thickness(10, 0, 0, 0); bannerActions.Children.Add(imageStatus); panel.Children.Add(bannerActions);
        Label(panel, "Tags · optional", 17); Label(panel, "Pick up to 30. Your main category decides where the plugin lives; tags help people find it.", 12, true);
        if (original != null) foreach (string tag in original.Tags) tags.Add(tag);
        var choices = new WrapPanel(); foreach (string tag in ModTaxonomy.Tags)
        {
            var toggle = new ToggleButton { Content = tag, IsChecked = tags.Contains(tag), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 6, 6), Foreground = Theme.Muted, Background = Theme.Raised, BorderThickness = new Thickness(0) };
            void Paint() { toggle.Foreground = toggle.IsChecked == true ? Theme.Accent : Theme.Muted; toggle.BorderBrush = Theme.Accent; toggle.BorderThickness = toggle.IsChecked == true ? new Thickness(1) : new Thickness(0); }
            toggle.Checked += (_, _) => { if (tags.Count >= 30 && !tags.Contains(tag)) { toggle.IsChecked = false; status.Text = "Choose up to 30 tags."; return; } tags.Add(tag); Paint(); };
            toggle.Unchecked += (_, _) => { tags.Remove(tag); Paint(); }; Paint(); choices.Children.Add(toggle);
        }
        panel.Children.Add(choices); Label(panel, "More tags, separated by commas"); extraTags.MaxLength = 1200; panel.Children.Add(extraTags);
        foreach (var pair in new[] { (name, "Plugin name"), (author, "Creator name"), (description, "Plugin description"), (extraTags, "Additional optional tags") }) AutomationProperties.SetName(pair.Item1, pair.Item2);
        CatalogMotion.Enter(panel, shell.Config);
    }
    ModDefinition Save()
    {
        if (template.SelectedItem is not ModDefinition source) throw new InvalidOperationException("Choose a starting plugin.");
        if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(author.Text) || main.SelectedItem is not string category || sub.SelectedItem is not string child) throw new InvalidOperationException("Add a name, creator, main category and subcategory.");
        var chosenTags = tags.Concat(extraTags.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (chosenTags.Count > 30) throw new InvalidOperationException("Choose up to 30 tags.");
        var candidate = Mods.Read(JsonSerializer.Serialize(source, Mods.Json)); candidate.Name = name.Text.Trim(); candidate.Author = author.Text.Trim(); candidate.Version = version.Text.Trim(); candidate.Description = description.Text.Trim(); candidate.Instructions = instructions.Text.Trim(); candidate.MainCategory = category; candidate.Subcategory = child; candidate.Tags = chosenTags; candidate.PreviewImage = image;
        foreach (var option in candidate.Options) option.Default = shell.Mods.Value(source, option.Key);
        Mods.Validate(candidate);
        if (created == null && original?.Id.StartsWith("local.") == true && source.Id == original.Id) created = original;
        if (created == null && sharing && source.Id.StartsWith("local.")) created = source;
        if (created == null) candidate.Id = "local.plugin." + Guid.NewGuid().ToString("N")[..12];
        else candidate.Id = created.Id;
        shell.Mods.UpdateDefinition(candidate); created = candidate; saved?.Invoke(candidate); return candidate;
    }
    void PickImage() => Attempt(() => {
        var dialog = new OpenFileDialog { Filter = "Banner images|*.png;*.jpg;*.jpeg" }; if (dialog.ShowDialog(this) != true) return;
        if (new FileInfo(dialog.FileName).Length > 20_000_000) throw new InvalidDataException("Choose an image under 20 MB.");
        using var input = File.OpenRead(dialog.FileName); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 960; bitmap.StreamSource = input; bitmap.EndInit();
        if (bitmap.PixelHeight > 4000) throw new InvalidDataException("Choose a landscape banner image.");
        var encoder = new JpegBitmapEncoder { QualityLevel = 82 }; encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = new MemoryStream(); encoder.Save(output);
        if (output.Length > 500_000) throw new InvalidDataException("The resized banner is too large. Choose a simpler image.");
        image = "data:image/jpeg;base64," + Convert.ToBase64String(output.ToArray()); imageStatus.Text = "Custom banner attached";
    });
    void Attempt(Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; } }
    static void Label(Panel panel, string text, double size = 13, bool muted = false) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, Foreground = muted ? Theme.Muted : Theme.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, size > 20 ? 0 : 13, 0, 8), FontWeight = size > 16 ? FontWeights.SemiBold : FontWeights.Normal });
}
