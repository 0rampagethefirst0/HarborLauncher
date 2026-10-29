using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace Harbor;

internal sealed class ModCatalog : Window
{
    readonly Shell shell;
    readonly Grid body = new();
    readonly TextBox search = new() { MinWidth = 220, Height = 36 };
    readonly TextBlock status = Theme.Label("", 12, Theme.Muted), count = Theme.Label("", 11, Theme.Muted);
    readonly StackPanel nav = new();
    readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(200) }, slides = new() { Interval = TimeSpan.FromSeconds(7) };
    readonly Dictionary<string, string> draft = new();
    string screen = "home", main = "", sub = "All", tag = "All tags", library = "Explore", selected = "";
    int slide, page;
    bool rendering;
    WindowEntry? app;
    const int PageSize = 12;
    internal ModCatalog(Shell shell)
    {
        this.shell = shell; Title = "Harbor — mods"; Theme.AppWindow(this, 1220, 850);
        MinWidth = 970; MinHeight = 710;
        var root = new DockPanel { Background = Theme.Bg };
        var header = Theme.Header(this, "Harbor Mods"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var footer = new DockPanel { Margin = new Thickness(22, 10, 22, 14) };
        count.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(count, System.Windows.Controls.Dock.Right); footer.Children.Add(count); status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0, 0, 15, 0); footer.Children.Add(status); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var layout = new Grid { Margin = new Thickness(18, 14, 22, 0) }; layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) }); layout.ColumnDefinitions.Add(new ColumnDefinition());
        var side = new DockPanel { Margin = new Thickness(0, 0, 20, 0) };
        var tools = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(tools, System.Windows.Controls.Dock.Bottom);
        tools.Children.Add(NavButton("Create a plugin", "mods", Create));
        tools.Children.Add(NavButton("Upload a plugin", "open", () => new ModCreator(shell, null, true) { Owner = this }.Show()));
        tools.Children.Add(NavButton("Community", "open", () => Community.Open("/")));
        tools.Children.Add(NavButton("Import package", "folder", Import));
        tools.Children.Add(NavButton("Desktop profiles", "profiles", shell.OpenProfiles)); side.Children.Add(tools); side.Children.Add(nav); layout.Children.Add(side);
        var content = new DockPanel(); Grid.SetColumn(content, 1);
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var refresh = Theme.Button(UiIcons.Icon("refresh"), "Refresh community plugins", async () => { status.Text = "Checking community plugins…"; try { int n = await Community.Refresh(shell.Mods); status.Text = $"Community refreshed · {n} plugins."; } catch (Exception ex) { status.Text = "Community: " + ex.Message; } }); refresh.Margin = new Thickness(12, 0, 0, 0); DockPanel.SetDock(refresh, System.Windows.Controls.Dock.Right); top.Children.Add(refresh);
        search.ToolTip = "Search every category, plugin and tag"; AutomationProperties.SetName(search, "Search mods"); top.Children.Add(Theme.Placeholder(search, "Search plugins, creators or tags")); DockPanel.SetDock(top, System.Windows.Controls.Dock.Top); content.Children.Add(top); content.Children.Add(body); layout.Children.Add(content); root.Children.Add(layout); Content = root;
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        debounce.Tick += (_, _) => { debounce.Stop(); if (search.Text.Trim().Length > 0) screen = "catalog"; page = 0; Render(); };
        slides.Tick += (_, _) => { if (screen == "showcase" && IsActive && !body.IsMouseOver && shell.Config.DockMotionMilliseconds > 0) { slide = (slide + 1) % Featured(main).Count; Render(); } };
        shell.Mods.Changed += OnChanged;
        Loaded += async (_, _) => { if (Settings.TestDataDir != null) return; try { await Community.Refresh(shell.Mods); } catch (Exception ex) { status.Text = "Offline catalog available. Community: " + ex.Message; } };
        Closed += (_, _) => { shell.Mods.Changed -= OnChanged; debounce.Stop(); slides.Stop(); };
        Activated += (_, _) => { if (screen == "showcase") slides.Start(); }; Deactivated += (_, _) => slides.Stop();
        Render();
    }
    Button NavButton(string text, string icon, Action click)
    {
        var b = Theme.Button(UiIcons.Label(text, 13, icon), text, click); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(10, 10, 8, 10); b.Margin = new Thickness(0, 2, 0, 2); return b;
    }
    void Navigate(string category, bool all = false)
    {
        main = category; sub = "All"; tag = "All tags"; page = 0; slide = 0; library = "Explore"; screen = all ? "catalog" : "showcase"; search.Clear(); debounce.Stop(); Render();
    }
    void Navigation()
    {
        nav.Children.Clear(); nav.Children.Add(NavButton("Discover", "mods", () => { main = ""; screen = "home"; search.Clear(); debounce.Stop(); library = "Explore"; Render(); }));
        foreach (string category in ModTaxonomy.Main)
        {
            var b = NavButton(category, category == "Desktop" ? "window" : category == "Themes" ? "appearance" : "controls", () => Navigate(category));
            if (main == category) { b.Background = Theme.Raised; b.Foreground = ModBanner.Accent(category); } nav.Children.Add(b);
        }
        nav.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(8, 16, 8, 12) });
        nav.Children.Add(NavButton("All plugins", "apps", () => Navigate("", true)));
        foreach (string v in new[] { "Installed", "Favorites", "Updates" })
        { var b = NavButton(v, v == "Favorites" ? "pin" : "mods", () => { main = ""; sub = "All"; tag = "All tags"; library = v; screen = "catalog"; page = 0; Render(); }); if (library == v) b.Background = Theme.Raised; nav.Children.Add(b); }
    }
    void Render(bool keepPosition = false)
    {
        if (rendering) return; rendering = true;
        try {
            double offset = keepPosition && body.Children.OfType<ScrollViewer>().FirstOrDefault() is ScrollViewer previous ? previous.VerticalOffset : 0;
            Dictionary<string, string>? edits = keepPosition && screen == "detail" ? new(draft) : null;
            slides.Stop(); body.Children.Clear(); Navigation();
            count.Text = $"{shell.Mods.Catalog.Count} packages · {shell.Mods.Catalog.Count(shell.Mods.Installed)} installed";
            var panel = new StackPanel(); var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 10, 0) }; body.Children.Add(scroll);
            if (screen == "home") Home(panel);
            else if (screen == "showcase") { Showcase(panel); if (IsActive) slides.Start(); }
            else if (screen == "detail") { var mod = shell.Mods.Catalog.FirstOrDefault(m => m.Id == selected); if (mod != null) Details(panel, mod, edits); else { screen = "catalog"; Catalog(panel); } }
            else Catalog(panel);
            if (keepPosition) scroll.ScrollToVerticalOffset(offset); else CatalogMotion.Enter(panel, shell.Config);
        } finally { rendering = false; }
    }
    void Home(StackPanel panel)
    {
        Text(panel, "A desktop that feels like you.", 30); Text(panel, "Three ways to make Harbor your own. Choose a category to see what’s possible.", 14, true);
        var grid = Columns(3);
        foreach (string category in ModTaxonomy.Main)
        {
            var content = new StackPanel(); content.Children.Add(new ModBanner(Featured(category)[0], shell, 172));
            var copy = new StackPanel { Margin = new Thickness(18) }; Text(copy, category, 24); Text(copy, ModTaxonomy.Description(category), 13, true);
            var subs = new WrapPanel(); foreach (string c in ModTaxonomy.Children[category].Take(4)) subs.Children.Add(Chip(c, null)); copy.Children.Add(subs);
            var next = Theme.Button(UiIcons.Label("Explore " + category, 13, "right"), "Explore " + category, () => Navigate(category)); next.Foreground = ModBanner.Accent(category); next.HorizontalAlignment = HorizontalAlignment.Left; next.Margin = new Thickness(0, 16, 0, 0); copy.Children.Add(next); content.Children.Add(copy);
            var card = Theme.Card(content, 16); card.Margin = new Thickness(0, 4, 12, 8); card.Cursor = System.Windows.Input.Cursors.Hand; card.MouseLeftButtonUp += (_, _) => Navigate(category); CatalogMotion.Hover(card, shell.Config); Grid.SetColumn(card, Array.IndexOf(ModTaxonomy.Main, category)); grid.Children.Add(card);
        }
        panel.Children.Add(grid);
        var creator = new Grid { Margin = new Thickness(0, 24, 12, 0) }; creator.ColumnDefinitions.Add(new ColumnDefinition()); creator.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var words = new StackPanel { Margin = new Thickness(20) }; Text(words, "Have an idea? Build it.", 20); Text(words, "Start with a working plugin, customize its settings or layout, then share your creation.", 13, true); creator.Children.Add(words);
        var create = Theme.Button("Create a plugin  →", "Create a plugin", Create); create.VerticalAlignment = VerticalAlignment.Center; create.Margin = new Thickness(12, 0, 20, 0); Grid.SetColumn(create, 1); creator.Children.Add(create); panel.Children.Add(Theme.Card(creator));
        Text(panel, "Included with Harbor", 19, marginTop: 24); Cards(panel, shell.Mods.Catalog.Where(m => new[] { "harbor.window-memory", "harbor.focus", "harbor.midnight" }.Contains(m.Id)).ToList());
    }
    List<ModDefinition> Featured(string category)
    {
        string[] ids = category switch { "Desktop" => new[] { "harbor.window-placement", "harbor.dock-scroll-windows", "harbor.window-opacity", "harbor.window-memory" }, "Themes" => new[] { "harbor.midnight", "harbor.forest", "harbor.ember", "harbor.plum" }, _ => new[] { "harbor.system", "harbor.day-planner", "harbor.sound-desk", "harbor.clock" } };
        var items = ids.Select(id => shell.Mods.Catalog.FirstOrDefault(m => m.Id == id)).OfType<ModDefinition>().ToList();
        return items.Count > 0 ? items : shell.Mods.Catalog.Where(m => ModTaxonomy.MainOf(m) == category).Take(4).ToList();
    }
    void Showcase(StackPanel panel)
    {
        Text(panel, main, 30); Text(panel, ModTaxonomy.Description(main), 14, true);
        var featured = Featured(main); slide = Math.Clamp(slide, 0, featured.Count - 1); var mod = featured[slide];
        var hero = new Grid { Margin = new Thickness(0, 4, 0, 0) }; hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.9, GridUnitType.Star) }); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        var copy = new StackPanel { Margin = new Thickness(27), VerticalAlignment = VerticalAlignment.Center }; Text(copy, $"FEATURED  /  {slide + 1:00}", 10, true); Text(copy, mod.Name, 29); Text(copy, mod.Description, 14, true);
        var actions = new WrapPanel(); actions.Children.Add(InstallButton(mod)); actions.Children.Add(Theme.Button("Details", "Open " + mod.Name, () => OpenDetails(mod))); copy.Children.Add(actions); hero.Children.Add(copy);
        var art = new Grid { Background = Theme.Brush(main == "Desktop" ? "#172F36" : main == "Themes" ? "#302642" : "#3A2C28"), MinHeight = 280 };
        art.Children.Add(new ModBanner(mod, shell, 260, true));
        var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 12) };
        for (int i = 0; i < featured.Count; i++)
        {
            int index = i; var dot = Theme.Button(new Ellipse { Width = 7, Height = 7, Fill = Brushes.White, Opacity = slide == i ? 1 : .35 }, "Show " + featured[i].Name, () => { slide = index; Render(); }); dot.Padding = new Thickness(7); dot.Background = Brushes.Transparent; dots.Children.Add(dot);
        }
        art.Children.Add(dots); Grid.SetColumn(art, 1); hero.Children.Add(art); panel.Children.Add(Theme.Card(hero, 18));
        var controls = new DockPanel { Margin = new Thickness(0, 15, 0, 18) };
        var browse = Theme.Button(UiIcons.Label("All " + main.ToLowerInvariant() + " mods", 14, "right"), "Browse all " + main + " mods", () => { screen = "catalog"; page = 0; Render(); }); browse.Foreground = ModBanner.Accent(main); DockPanel.SetDock(browse, System.Windows.Controls.Dock.Right); controls.Children.Add(browse);
        var previous = Theme.Button(UiIcons.Icon("left"), "Previous slide", () => { slide = (slide + featured.Count - 1) % featured.Count; Render(); }); previous.HorizontalAlignment = HorizontalAlignment.Left; controls.Children.Add(previous);
        var next = Theme.Button(UiIcons.Icon("right"), "Next slide", () => { slide = (slide + 1) % featured.Count; Render(); }); next.HorizontalAlignment = HorizontalAlignment.Left; controls.Children.Add(next); panel.Children.Add(controls);
        Text(panel, "Explore " + main.ToLowerInvariant(), 19);
        var categories = new WrapPanel(); foreach (string child in ModTaxonomy.Children[main]) { string chosen = child; categories.Children.Add(Chip(child + "  →", () => { sub = chosen; screen = "catalog"; page = 0; Render(); })); } panel.Children.Add(categories);
        Text(panel, "Try something new", 19, marginTop: 18); Cards(panel, featured.Where(m => m.Id != mod.Id).Take(3).ToList());
    }
    IEnumerable<ModDefinition> Matches()
    {
        string term = search.Text.Trim(); return shell.Mods.Catalog.Where(m => (main.Length == 0 || ModTaxonomy.MainOf(m) == main) && (sub == "All" || ModTaxonomy.SubOf(m) == sub) && (tag == "All tags" || m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) &&
            (m.Name + " " + m.Id + " " + m.Author + " " + m.Description + " " + ModTaxonomy.MainOf(m) + " " + ModTaxonomy.SubOf(m) + " " + string.Join(" ", m.Tags)).Contains(term, StringComparison.CurrentCultureIgnoreCase) &&
            (library switch { "Installed" => shell.Mods.Installed(m), "Enabled" => shell.Mods.EnabledMod(m), "Updates" => shell.Mods.UpdateAvailable(m), "Favorites" => shell.Config.FavoriteMods.Contains(m.Id), "Behavior mods" => m.Kind == "tweak", "Widgets" => m.Kind == "widget", _ => true }));
    }
    void Catalog(StackPanel panel)
    {
        var mods = Matches().ToList(); int pages = Math.Max(1, (mods.Count + PageSize - 1) / PageSize); page = Math.Clamp(page, 0, pages - 1);
        Text(panel, library != "Explore" ? library : main.Length > 0 ? main + " mods" : "All plugins", 28); Text(panel, $"{mods.Count} packages · Browse, install, make them yours.", 13, true);
        var filters = new WrapPanel { Margin = new Thickness(0, 3, 0, 14) };
        string[] subs = main.Length > 0 ? ModTaxonomy.Children[main] : ModTaxonomy.Children.Values.SelectMany(x => x).Distinct().ToArray();
        var subBox = new ComboBox { ItemsSource = new[] { "All" }.Concat(subs).ToArray(), SelectedItem = sub, MinWidth = 160, Margin = new Thickness(0, 0, 10, 0) }; AutomationProperties.SetName(subBox, "Subcategory"); subBox.SelectionChanged += (_, _) => { sub = subBox.SelectedItem as string ?? "All"; page = 0; Render(); }; filters.Children.Add(subBox);
        var tags = shell.Mods.Catalog.Where(m => main.Length == 0 || ModTaxonomy.MainOf(m) == main).SelectMany(m => m.Tags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();
        var tagBox = new ComboBox { ItemsSource = new[] { "All tags" }.Concat(tags).ToArray(), SelectedItem = tag, MinWidth = 155, Margin = new Thickness(0, 0, 10, 0) }; AutomationProperties.SetName(tagBox, "Optional tag filter"); tagBox.SelectionChanged += (_, _) => { tag = tagBox.SelectedItem as string ?? "All tags"; page = 0; Render(); }; filters.Children.Add(tagBox);
        if (main.Length > 0) filters.Children.Add(Theme.Button("Showcase", "Back to category showcase", () => { screen = "showcase"; Render(); })); panel.Children.Add(filters);
        if (mods.Count == 0) { Text(panel, library == "Updates" ? "You’re up to date." : "No plugins match these filters.", 19); panel.Children.Add(Theme.Button("Clear filters", "Clear all filters", () => Navigate(main, true))); return; }
        Cards(panel, mods.Skip(page * PageSize).Take(PageSize).ToList());
        var pager = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 10) };
        var prev = Theme.Button("← Previous", "Previous page", () => { page--; Render(); }); prev.IsEnabled = page > 0; pager.Children.Add(prev); var label = Theme.Label($"  {page + 1} / {pages}  ", 13, Theme.Muted); label.Margin = new Thickness(12, 0, 12, 0); pager.Children.Add(label); var next = Theme.Button("Next →", "Next page", () => { page++; Render(); }); next.IsEnabled = page + 1 < pages; pager.Children.Add(next); panel.Children.Add(pager);
    }
    void Cards(StackPanel panel, List<ModDefinition> mods)
    {
        var grid = Columns(3);
        for (int index = 0; index < mods.Count; index++)
        {
            var mod = mods[index]; int row = index / 3; if (row >= grid.RowDefinitions.Count) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var content = new StackPanel(); var image = new ModBanner(mod, shell, 143); image.Cursor = System.Windows.Input.Cursors.Hand; image.MouseLeftButtonUp += (_, _) => OpenDetails(mod); content.Children.Add(image);
            var copy = new StackPanel { Margin = new Thickness(15, 12, 15, 15) }; var title = Theme.Button(mod.Name, "Details for " + mod.Name, () => OpenDetails(mod)); title.FontSize = 16; title.FontWeight = FontWeights.SemiBold; title.Padding = new Thickness(0); title.HorizontalContentAlignment = HorizontalAlignment.Left; copy.Children.Add(title);
            Text(copy, ModTaxonomy.SubOf(mod) + " · " + mod.Author, 11, true); var desc = Theme.Label(mod.Description, 12, Theme.Muted); desc.TextWrapping = TextWrapping.Wrap; desc.Height = 38; desc.TextTrimming = TextTrimming.CharacterEllipsis; copy.Children.Add(desc);
            var tags = new WrapPanel { Margin = new Thickness(0, 8, 0, 9), Height = 28, ClipToBounds = true }; foreach (string t in mod.Tags.Take(3)) tags.Children.Add(Chip(t, () => { tag = t; screen = "catalog"; page = 0; Render(); }, true)); copy.Children.Add(tags);
            var buttons = new DockPanel(); var detail = Theme.Button(UiIcons.Icon("controls"), "Settings for " + mod.Name, () => OpenDetails(mod)); DockPanel.SetDock(detail, System.Windows.Controls.Dock.Right); buttons.Children.Add(detail); var install = InstallButton(mod); install.HorizontalAlignment = HorizontalAlignment.Left; buttons.Children.Add(install); copy.Children.Add(buttons); content.Children.Add(copy);
            var card = Theme.Card(content, 14); card.Margin = new Thickness(0, 0, 12, 14); CatalogMotion.Hover(card, shell.Config); Grid.SetColumn(card, index % 3); Grid.SetRow(card, row); grid.Children.Add(card);
        }
        panel.Children.Add(grid);
    }
    Button InstallButton(ModDefinition mod)
    {
        bool installed = shell.Mods.Installed(mod); var plan = installed ? null : shell.Mods.PlanEnable(mod);
        var button = Theme.Button(installed ? "Uninstall" : "Install", installed ? "Uninstall " + mod.Name : plan?.Conflicts.Count > 0 ? "Install and replace " + string.Join(", ", plan.Conflicts.Select(m => m.Name)) : "Install and enable " + mod.Name, () => Try(() => {
            if (installed) { shell.Mods.Uninstall(mod); status.Text = mod.Name + " uninstalled. Your settings are kept."; }
            else { if (screen == "detail" && selected == mod.Id && draft.Count > 0) Commit(mod); shell.Mods.Enable(mod, true, true); status.Text = mod.Name + " installed and enabled."; }
        }));
        button.Background = installed ? Theme.Raised : ModBanner.Accent(ModTaxonomy.MainOf(mod)); button.Foreground = installed ? Theme.Text : Theme.Brush("#16202D"); button.Margin = new Thickness(0, 0, 8, 0); button.IsEnabled = installed || plan?.Errors.Count == 0; if (!button.IsEnabled) button.ToolTip = string.Join(" ", plan!.Errors); return button;
    }
    void OpenDetails(ModDefinition mod) { selected = mod.Id; screen = "detail"; Render(); }
    void Details(StackPanel panel, ModDefinition mod, Dictionary<string, string>? edits = null)
    {
        var back = Theme.Button("← Back to mods", "Back to mods", () => { screen = "catalog"; Render(); }); back.HorizontalAlignment = HorizontalAlignment.Left; back.Margin = new Thickness(0, 0, 0, 15); panel.Children.Add(back);
        Text(panel, mod.Name, 30, marginTop: 0); Text(panel, $"{mod.Author} · {ModTaxonomy.MainOf(mod)} / {ModTaxonomy.SubOf(mod)} · v{mod.Version}", 13, true); Text(panel, mod.Description, 15);
        var hero = new Grid { Margin = new Thickness(0, 12, 0, 16) }; hero.ColumnDefinitions.Add(new ColumnDefinition()); hero.ColumnDefinitions.Add(new ColumnDefinition());
        hero.Children.Add(new ModBanner(mod, shell, 280, true) { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center }); var usage = UsageCard(mod); Grid.SetColumn(usage, 1); hero.Children.Add(usage); panel.Children.Add(hero);
        var tags = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) }; foreach (string t in mod.Tags) tags.Children.Add(Chip(t, () => { tag = t; main = ""; sub = "All"; screen = "catalog"; page = 0; Render(); }, true)); panel.Children.Add(tags);
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 15) }; actions.Children.Add(InstallButton(mod));
        actions.Children.Add(Theme.Button("Larger preview", "Open a larger animated demonstration", () => new ModPreviewWindow(shell, mod) { Owner = this }.Show()));
        if (shell.Mods.Installed(mod)) actions.Children.Add(Theme.Button(shell.Mods.EnabledMod(mod) ? "Pause" : "Resume", "Toggle this mod without uninstalling", () => Try(() => { Commit(mod); shell.Mods.Enable(mod, !shell.Mods.EnabledMod(mod), true); })));
        if (shell.Mods.UpdateAvailable(mod)) actions.Children.Add(Theme.Button("Update", "Update this mod", () => Try(() => { if (shell.Mods.EnabledMod(mod)) shell.Mods.Enable(mod, true, true); else shell.Mods.Install(mod); })));
        actions.Children.Add(Theme.Button(shell.Config.FavoriteMods.Contains(mod.Id) ? "Unfavorite" : "Favorite", "Toggle favorite", () => Try(() => { if (!shell.Config.FavoriteMods.Remove(mod.Id)) shell.Config.FavoriteMods.Add(mod.Id); shell.Config.Save(); Render(); })));
        actions.Children.Add(Theme.Button("Make your own", "Create an independent plugin", () => new ModCreator(shell, mod, false, created => OpenDetails(created)) { Owner = this }.Show()));
        actions.Children.Add(Theme.Button("Export", "Export this plugin", () => Export(mod)));
        if (mod.Id.StartsWith("local.")) actions.Children.Add(Theme.Button("Edit / upload", "Edit your plugin and upload it", () => new ModCreator(shell, mod, true, created => OpenDetails(created)) { Owner = this }.Show())); panel.Children.Add(actions);
        var plan = shell.Mods.PlanEnable(mod); foreach (string error in plan.Errors) Text(panel, error, 12, true); if (plan.Conflicts.Count > 0) Text(panel, "Installing replaces: " + string.Join(", ", plan.Conflicts.Select(m => m.Name)), 12, true); if (mod.Dependencies.Count > 0) Text(panel, "Requires: " + string.Join(", ", mod.Dependencies.Select(d => d.Id)), 12, true);
        if (mod.Kind == "tweak") Text(panel, shell.Tweaks.Status(mod), 12, true);
        if (mod.Kind == "widget") panel.Children.Add(Theme.Button("Open visual builder", "Design the layout", () => new ModDesigner(shell, mod) { Owner = this }.Show()));
        if (mod.Kind == "tweak" && Tweaks.WindowComponents.Contains(mod.Component))
        {
            if (app != null) panel.Children.Add(Theme.Button("Create rule for " + app.ProcessName, "Create a separate app rule", () => Try(() => { var created = shell.Mods.Create(mod, mod.Name + " — " + app.ProcessName, "Local"); shell.Mods.Set(created, "apps", app.AppId.Length > 0 ? app.AppId : Path.GetFileName(app.Path), false); OpenDetails(created); })));
            Text(panel, "App patterns match executable names, full paths or packaged app IDs. * matches any text; ? matches one character. A blank app list matches nothing. Window effects pause on the active game’s monitor.", 12, true, 10);
        }
        draft.Clear(); foreach (var option in mod.Options) draft[option.Key] = edits?.GetValueOrDefault(option.Key) ?? shell.Mods.Value(mod, option.Key);
        if (mod.Options.Count > 0) Text(panel, "Plugin settings", 20, marginTop: 20);
        foreach (var option in mod.Options)
        {
            Text(panel, option.Label, 13, marginTop: 8);
            Text(panel, ModHelp.OptionHint(mod, option), 13, true);
            if (option.Type == "choice") { var box = new ComboBox { ItemsSource = option.Choices, SelectedItem = draft[option.Key], MinHeight = 34, MaxWidth = 580, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 260 }; AutomationProperties.SetName(box, option.Label); box.SelectionChanged += (_, _) => { if (box.SelectedItem is string value) draft[option.Key] = value; }; panel.Children.Add(box); }
            else { var box = new TextBox { Text = draft[option.Key], AcceptsReturn = option.Type == "multiline", MinHeight = option.Type == "multiline" ? 90 : 34, MaxLength = 10000, TextWrapping = TextWrapping.Wrap, MaxWidth = 730, HorizontalAlignment = HorizontalAlignment.Stretch }; AutomationProperties.SetName(box, option.Label); box.TextChanged += (_, _) => draft[option.Key] = box.Text; panel.Children.Add(box); }
        }
        if (mod.Options.Count > 0) { var buttons = new WrapPanel { Margin = new Thickness(0, 20, 0, 20) }; buttons.Children.Add(Theme.Button("Apply settings", "Apply plugin settings", () => Try(() => { Commit(mod); shell.RebuildMods(); status.Text = "Settings applied."; }))); buttons.Children.Add(Theme.Button("Reset", "Reset plugin settings", () => Try(() => { shell.Mods.Configure(mod, mod.Options.ToDictionary(o => o.Key, o => o.Default)); shell.RebuildMods(); Render(); }))); panel.Children.Add(buttons); }
    }
    void Create() => new ModCreator(shell, null, false, created => OpenDetails(created)) { Owner = this }.Show();
    static FrameworkElement UsageCard(ModDefinition mod)
    {
        var guide = new StackPanel { Margin = new Thickness(20) }; guide.Children.Add(Theme.Label("How to use", 22)); int stepNumber = 0;
        foreach (string step in ModHelp.Steps(mod)) { var row = new DockPanel { Margin = new Thickness(0, 14, 0, 0) }; var number = Theme.Label((++stepNumber).ToString(), 15, Theme.Accent); number.Width = 28; number.VerticalAlignment = VerticalAlignment.Top; row.Children.Add(number); row.Children.Add(new TextBlock { Text = step, Foreground = Theme.Text, FontSize = 15, TextWrapping = TextWrapping.Wrap, LineHeight = 24 }); guide.Children.Add(row); }
        return new Border { Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = guide };
    }
    void Export(ModDefinition mod)
    { var dialog = new SaveFileDialog { Filter = "Harbor plugin|*.harbormod", FileName = mod.Id + ".harbormod" }; if (dialog.ShowDialog(this) == true) Try(() => { if (screen == "detail") Commit(mod); shell.Mods.ExportConfigured(mod, dialog.FileName); status.Text = "Package exported."; }); }
    void Import()
    { var dialog = new OpenFileDialog { Filter = "Harbor plugins and catalogs|*.harbormod;*.json;*.zip" }; if (dialog.ShowDialog(this) == true) Try(() => { int n = shell.Mods.Import(dialog.FileName); status.Text = $"{n} packages imported. Resume an imported plugin to enable it."; library = "Installed"; screen = "catalog"; Render(); }); }
    void Commit(ModDefinition mod) { if (draft.Count > 0) shell.Mods.Configure(mod, new Dictionary<string, string>(draft)); }
    void OnChanged() => Render(true);
    void Try(Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; } }
    static Grid Columns(int number) { var grid = new Grid(); for (int i = 0; i < number; i++) grid.ColumnDefinitions.Add(new ColumnDefinition()); return grid; }
    static void Text(Panel panel, string text, double size, bool muted = false, double marginTop = 0) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, Foreground = muted ? Theme.Muted : Theme.Text, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5, FontWeight = size >= 19 ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, marginTop, 0, size >= 19 ? 7 : 12) });
    static FrameworkElement Chip(string text, Action? click, bool small = false)
    {
        var label = Theme.Label(text, small ? 10 : 12, Theme.Muted);
        if (click != null) { var button = Theme.Button(label, "Filter by " + text, click); button.Background = Theme.Raised; button.Padding = new Thickness(small ? 7 : 12, small ? 4 : 8, small ? 7 : 12, small ? 4 : 8); button.Margin = new Thickness(0, 0, 7, 7); return button; }
        return new Border { Background = Theme.Raised, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 6, 6), Child = label };
    }
    internal void PreparePreview(string kind)
    {
        if (kind.StartsWith("category:")) { main = kind[9..]; screen = "showcase"; }
        else if (kind.StartsWith("detail:")) { selected = kind[7..]; screen = "detail"; }
        else if (kind.StartsWith("catalog:")) { main = kind[8..]; screen = "catalog"; }
        else if (kind == "Behavior mods") { selected = "harbor.window-placement"; screen = "detail"; main = "Desktop"; }
        else if (kind == "Installed") { screen = "catalog"; library = kind; }
        Render();
    }
    internal void ShowAppRules(WindowEntry window) { app = window; main = "Desktop"; library = "Behavior mods"; selected = "harbor.window-topmost"; screen = "detail"; Render(); }
}
