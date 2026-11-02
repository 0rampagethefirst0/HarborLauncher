using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Harbor;

internal static class CatalogVerification
{
    internal static void Run(Shell shell, Action<string, bool, object?> check)
    {
        var mods = shell.Mods; var bundled = mods.Catalog.Where(m => m.Id.StartsWith("harbor.")).ToList();
        check("All preset packages have a main category, valid subcategory and tags", bundled.Count == 37 + ShellFeatures.Definitions.Length && bundled.All(m => ModTaxonomy.Main.Contains(m.MainCategory) && ModTaxonomy.Children[m.MainCategory].Contains(m.Subcategory) && m.Tags.Count >= 3), null);
        var old = Mods.Read("{\"id\":\"test.legacy\",\"name\":\"Legacy clock\",\"component\":\"clock\",\"category\":\"Desktop\"}");
        check("Old manifests keep working with automatic category mapping", ModTaxonomy.MainOf(old) == "Tools" && ModTaxonomy.SubOf(old) == "Time", null);
        var invalid = Mods.Read(JsonSerializer.Serialize(old)); invalid.MainCategory = "Not a category";
        check("Unknown main categories are rejected", Reject(() => Mods.Validate(invalid)), null);
        invalid.MainCategory = "Themes"; invalid.Subcategory = "Windows";
        check("Subcategories cannot cross main categories", Reject(() => Mods.Validate(invalid)), null);
        invalid.MainCategory = "Tools"; invalid.Subcategory = "Time"; invalid.PreviewImage = "data:image/png;base64,AAAA";
        check("Invalid banner content is rejected before installing", Reject(() => Mods.Validate(invalid)), null);
        var note = bundled.First(m => m.Component == "notes"); var custom = mods.Create(note, "AAA Catalog click test", "Verification"); mods.Set(custom, "width", "349", false); mods.Uninstall(custom);
        var window = new ModCatalog(shell);
        foreach (string category in ModTaxonomy.Main) {
            window.PreparePreview("category:" + category);
            check(category + " showcase has four white slide indicators and a browse arrow", Buttons(window).Count(b => AutomationProperties.GetName(b).StartsWith("Show ")) == 4 && Buttons(window).Any(b => AutomationProperties.GetName(b) == "Browse all " + category + " mods"), null);
        }
        window.PreparePreview("catalog:Tools");
        Buttons(window).Single(b => AutomationProperties.GetName(b) == "Install and enable " + custom.Name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check("A catalog Install click immediately installs and enables the package", mods.Installed(custom) && mods.EnabledMod(custom), null);
        Buttons(window).Single(b => AutomationProperties.GetName(b) == "Uninstall " + custom.Name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check("The same card becomes Uninstall and preserves settings after removal", !mods.Installed(custom) && !mods.EnabledMod(custom) && mods.Value(custom, "width") == "349", null);
        window.PreparePreview("Behavior mods");
        var placement = bundled.First(m => m.Component == "window-placement"); var option = placement.Options.First(o => o.Type != "choice"); string persisted = mods.Value(placement, option.Key);
        Fields(window).Single(box => AutomationProperties.GetName(box) == option.Label).Text = "Unfinished catalog edit";
        mods.NotifyChanged();
        check("Catalog refresh preserves unfinished settings without saving them", Fields(window).Single(box => AutomationProperties.GetName(box) == option.Label).Text == "Unfinished catalog edit" && mods.Value(placement, option.Key) == persisted, null);
        window.Close();
        var community = Mods.Read(JsonSerializer.Serialize(old)); community.Id = "local.community-test"; community.MainCategory = "Tools"; community.Subcategory = "Time"; community.Tags = new() { "clock", "minimal" };
        mods.ReplaceCommunity(new() { community });
        check("Community refresh adds available packages without running them", mods.Catalog.Any(m => m.Id == community.Id) && !mods.Installed(community) && !mods.EnabledMod(community), null);
        string cache = Path.Combine(Settings.DataDir, "Catalogs", "community.json"), before = File.ReadAllText(cache);
        check("Community packages cannot replace built-in IDs or change a valid cache", Reject(() => mods.ReplaceCommunity(new() { bundled[0] })) && File.ReadAllText(cache) == before, null);
        mods.ReplaceCommunity(new());
        check("Removed community packages leave the available catalog", mods.Catalog.All(m => m.Id != community.Id), null);
    }
    static bool Reject(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
    static IEnumerable<Button> Buttons(DependencyObject node)
    {
        if (node is Button button) yield return button;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var b in Buttons(child)) yield return b;
    }
    static IEnumerable<TextBox> Fields(DependencyObject node)
    {
        if (node is TextBox box) yield return box;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var field in Fields(child)) yield return field;
    }
}
