using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Harbor;

internal sealed class HarborHome : Window
{
    const string ItemDrag = "Harbor.HomeItem";
    readonly Shell shell;
    readonly IntPtr monitor;
    readonly bool demo;
    readonly Grid panel = new(), cards = new();
    readonly StackPanel sidebar = new();
    readonly TextBox search = new();
    readonly TextBlock heading = Theme.Label("Desktop", 28), summary = Theme.Label("", 13, Theme.Muted), footer = Theme.Label("", 12, Theme.Muted);
    readonly Canvas stage = new();
    readonly SolidColorBrush frameBrush = new(Color.FromRgb(16, 27, 43)) { Opacity = 0 }, frameEdge = new(Color.FromRgb(43, 71, 102)) { Opacity = 0 };
    readonly Border scrim = new() { Background = Brushes.Transparent };
    readonly Border frame;
    readonly HarborEmblem flying = new();
    readonly HarborEmblem headerEmblem = new(48);
    readonly DragonHomeFire dragonFire = new();
    readonly Image dragonImage = new() { Stretch = Stretch.Fill, IsHitTestVisible = false, Visibility = Visibility.Hidden };
    DragonFrameCache.Frames? dragonFrames;
    readonly SolidColorBrush dragonCurtainBrush = new();
    readonly Border dragonCurtain = new() { IsHitTestVisible = false, Visibility = Visibility.Hidden };
    internal object? DragonCache => dragonFrames == null ? null : new { Frames = dragonFrames.Items.Length, dragonFrames.PreparationMilliseconds, Megabytes = dragonFrames.Bytes / 1048576d };
    readonly DockPanel header = new();
    readonly ScaleTransform flyScale = new();
    readonly TranslateTransform flyOffset = new();
    readonly FrameTiming frames = new();
    readonly Grid layers = new();
    readonly Image snapshot = new() { Stretch = Stretch.Fill, Visibility = Visibility.Hidden, IsHitTestVisible = false };
    BitmapSource? savedPanel;
    bool cacheDirty = true, cacheScheduled;
    bool inMotion, settled;
    bool firstFrameReady, paintCloaked, cloakApplied, nativeTransitionsDisabled;
    double? firstFrameReveal, animationStartReveal;
    readonly System.Windows.Threading.DispatcherTimer paintTimeout = new() { Interval = TimeSpan.FromSeconds(3) };
    internal object PresentationState => new { FirstFrameReady = firstFrameReady, PaintCloaked = paintCloaked, CloakApplied = cloakApplied, NativeTransitionsDisabled = nativeTransitionsDisabled, FirstFrameReveal = firstFrameReveal, AnimationStartReveal = animationStartReveal, DragonFrames = DragonCache };
    internal object FramePacing => frames.Report();
    internal bool UsesCachedMorph { get; private set; }
    internal bool Ready => IsVisible && settled && !closing;
    internal bool Transitioning => IsVisible && !closed && (inMotion || !settled && !closing);
    internal bool Prepared => savedPanel != null && !cacheDirty;
    internal double SnapshotMilliseconds { get; private set; }
    internal event Action? Opened;
    readonly TranslateTransform offset = new(), bodyOffset = new();
    readonly TranslateTransform contentOffset = new();
    readonly RectangleGeometry viewport = new();
    readonly Grid contents = new();
    List<DesktopLibraryItem> desktop = new();
    string category = "All", source = "Desktop";
    int page;
    bool closing, closed;
    Point origin;
    double originSize;
    readonly double originTurn;
    internal EmblemState FlyingState => new(new Rect(flyOffset.X, flyOffset.Y, flying.Width * flyScale.ScaleX, flying.Height * flyScale.ScaleY), flying.Turn);
    internal double LogoOpacity => flying.Opacity;
    internal IntPtr MonitorHandle => monitor;
    internal int ItemCount => Items.Count;
    internal string CurrentCategory => category;
    internal string CurrentSource => source;
    internal int VisibleTiles => cards.Children.OfType<Button>().Count();
    internal void SearchPreview(string text) => search.Text = text;
    List<DesktopLibraryItem> Items => source == "Desktop" ? desktop : demo ? DemoItems() : source == "Frequently used" ? desktop.Concat(DesktopLibraryModel.AllApps(shell.Catalog.Apps)).DistinctBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList() : DesktopLibraryModel.AllApps(shell.Catalog.Apps);
    internal static readonly DependencyProperty RevealProperty = DependencyProperty.Register(nameof(Reveal), typeof(double), typeof(HarborHome), new PropertyMetadata(0d, (d, _) => ((HarborHome)d).ArrangeReveal()));
    internal double Reveal { get => (double)GetValue(RevealProperty); set => SetValue(RevealProperty, value); }
    internal HarborHome(Shell shell, IntPtr monitor, EmblemState? logo = null, bool demo = false, IReadOnlyList<DesktopLibraryItem>? items = null)
    {
        this.shell = shell; this.monitor = monitor; this.demo = demo; Title = "Harbor Home";
        var work = PluginSnapping.WorkArea(shell, monitor); double dpi = Native.Scale(monitor);
        var display = Native.Monitor(monitor).Monitor;
        Width = demo ? 1280 : work.Width / dpi; Height = demo ? 900 : work.Height / dpi; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Background = demo ? HarborBackdrop.Brush(1280, 900) : HarborBackdrop.Brush(display.Width, display.Height, new Rect(work.Left - display.Left, work.Top - display.Top, work.Width, work.Height)); ShowInTaskbar = false; Topmost = true;
        double width = Math.Min(shell.Config.HomeWidth, Width - 36), height = Math.Min(shell.Config.HomeHeight, Height - 36); panel.Width = width; panel.Height = height;
        var localLogo = logo?.RelativeTo(new Point(work.Left, work.Top), dpi);
        origin = localLogo?.Center ?? (demo ? new Point(Width / 2, Height / 2) : new Point((display.Left + display.Width / 2d - work.Left) / dpi, (display.Top + display.Height / 2d - work.Top) / dpi));
        originSize = localLogo?.Bounds.Width ?? shell.Config.DesktopLogoSize; originTurn = localLogo?.Turn ?? -16; headerEmblem.Turn = originTurn;
        flying.Width = flying.Height = originSize;
        flying.RenderTransform = new TransformGroup { Children = { flyScale, flyOffset } };
        var root = new Grid(); scrim.MouseLeftButtonDown += (_, _) => Dismiss(); root.Children.Add(scrim); root.Children.Add(stage); Content = root;
        layers.Children.Add(panel); layers.Children.Add(snapshot);
        // Content already has a 26px inset. A changing full-panel clipping mask
        // forces an extra software surface for every frame of the scale.
        frameBrush.Color = Theme.Panel.Color; frameEdge.Color = Theme.Line.Color;
        frame = new Border { Width = width, Height = height, Background = frameBrush, BorderBrush = frameEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(shell.Config.HomeCornerRadius) };
        RenderOptions.SetBitmapScalingMode(snapshot, BitmapScalingMode.LowQuality);
        frame.RenderTransform = offset; stage.Children.Add(frame);
        layers.Width = width; layers.Height = height; layers.RenderTransform = contentOffset; layers.Clip = viewport; stage.Children.Add(layers);
        flying.IsHitTestVisible = false; stage.Children.Add(flying); stage.Children.Insert(0, dragonFire); dragonFire.Width = Width; dragonFire.Height = Height; dragonFire.Origin = origin; dragonFire.LogoSize = originSize; dragonFire.CornerRadius = shell.Config.HomeCornerRadius; dragonFire.HomeBounds = new Rect((Width - width) / 2, (Height - height) / 2, width, height);
        var effectBounds = dragonFire.HomeBounds; effectBounds.Union(new Rect(origin.X - originSize, origin.Y - originSize, originSize * 2, originSize * 2)); effectBounds.Inflate(28, 28); effectBounds.Intersect(new Rect(0, 0, Width, Height)); dragonFire.Width = effectBounds.Width; dragonFire.Height = effectBounds.Height; dragonFire.RenderOffset = effectBounds.TopLeft; Canvas.SetLeft(dragonFire, effectBounds.Left); Canvas.SetTop(dragonFire, effectBounds.Top);
        dragonFire.LogoSize = shell.Config.DesktopLogoSize; stage.Children.Insert(1, dragonImage); RenderOptions.SetBitmapScalingMode(dragonImage, BitmapScalingMode.LowQuality);
        Build(); panel.Children.Add(contents); dragonCurtain.Background = dragonCurtainBrush; dragonCurtain.CornerRadius = new CornerRadius(shell.Config.HomeCornerRadius); stage.Children.Add(dragonCurtain);
        contents.RenderTransform = bodyOffset;
        SourceInitialized += (_, _) =>
        {
            Native.ToolWindow(this); var handle = new WindowInteropHelper(this).Handle;
            nativeTransitionsDisabled = Native.DisableWindowTransitions(handle);
            // DWM keeps composing a cloaked window, without exposing an unpainted
            // surface. Only this Home window is cloaked, for its initial paint.
            cloakApplied = paintCloaked = Native.CloakWindow(handle, true);
            Native.SetWindowPos(handle, new IntPtr(-1), (int)work.Left, (int)work.Top, (int)work.Width, (int)work.Height, 0x10);
        };
        Loaded += (_, _) => { if (!firstFrameReady && !closed) paintTimeout.Start(); };
        paintTimeout.Tick += (_, _) => { paintTimeout.Stop(); if (!firstFrameReady && !closed) { Log.Write("Home first paint timed out; returning to the desktop."); Dismiss(false); } };
        ContentRendered += (_, _) =>
        {
            if (firstFrameReady || closed || closing) return;
            paintTimeout.Stop(); firstFrameReveal = Reveal;
            if (paintCloaked)
            {
                Native.DwmFlush();
                if (!Native.CloakWindow(new WindowInteropHelper(this).Handle, false)) { Log.Write("Home could not reveal its prepared window."); Dismiss(false); return; }
                paintCloaked = false;
            }
            firstFrameReady = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { if (!closed && !closing) Animate(true); }));
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; } else if (e.Key == Key.F && System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { search.Focus(); search.SelectAll(); e.Handled = true; } };
        Closed += (_, _) => { closed = true; paintTimeout.Stop(); BeginAnimation(RevealProperty, null); shell.Catalog.Changed -= CatalogChanged; };
        shell.Catalog.Changed += CatalogChanged;
        if (demo) desktop = DemoItems();
        else if (items != null) desktop = items.ToList();
        source = demo ? "Desktop" : shell.Config.HomeStartTab;
        Render(); ArrangeReveal();
    }
    void Build()
    {
        contents.Margin = new Thickness(26); contents.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); contents.RowDefinitions.Add(new RowDefinition()); contents.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.Height = 48;
        var close = Theme.Button(UiIcons.Icon("close", 18), "Close Harbor Home", () => Dismiss()); close.Padding = new Thickness(9); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close);
        var customize = Theme.Button(UiIcons.Icon("controls", 18), "Customize Harbor Home", () => { Dismiss(false); shell.OpenHomeSettings(); }); customize.Padding = new Thickness(9); DockPanel.SetDock(customize, System.Windows.Controls.Dock.Right); header.Children.Add(customize);
        headerEmblem.Visibility = Visibility.Hidden; headerEmblem.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(headerEmblem, System.Windows.Controls.Dock.Left); header.Children.Add(headerEmblem);
        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; identity.Children.Add(Theme.Label("Harbor Home", 20)); identity.Children.Add(Theme.Label("Apps, files and folders", 12, Theme.Muted)); header.Children.Add(identity); stage.Children.Add(header);
        contents.Children.Add(new Border { Height = 73, IsHitTestVisible = false });
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(shell.Config.HomeSidebarWidth) }); layout.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(layout, 1); contents.Children.Add(layout);
        var rail = new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 22, 0) }; layout.Children.Add(rail);
        var main = new DockPanel(); Grid.SetColumn(main, 1); layout.Children.Add(main);
        var top = new StackPanel(); top.Children.Add(heading); summary.Margin = new Thickness(0, 7, 0, 18); top.Children.Add(summary);
        search.FontSize = 14; search.Padding = new Thickness(13, 10, 13, 10); AutomationProperties.SetName(search, "Search Harbor Home"); top.Children.Add(Theme.Placeholder(search, "Search your apps, folders and files")); search.TextChanged += (_, _) => { page = 0; RenderCards(); };
        top.Margin = new Thickness(0, 0, 20, 18); DockPanel.SetDock(top, System.Windows.Controls.Dock.Top); main.Children.Add(top);
        var pager = new DockPanel { Margin = new Thickness(0, 10, 10, 0) }; var next = Theme.Button("Next →", "Next page", () => { page++; RenderCards(); }); DockPanel.SetDock(next, System.Windows.Controls.Dock.Right); pager.Children.Add(next); var previous = Theme.Button("← Previous", "Previous page", () => { page = Math.Max(0, page - 1); RenderCards(); }); DockPanel.SetDock(previous, System.Windows.Controls.Dock.Right); pager.Children.Add(previous); pager.Children.Add(footer); DockPanel.SetDock(pager, System.Windows.Controls.Dock.Bottom); main.Children.Add(pager);
        var scroll = new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Motion.SmoothScroll(scroll); main.Children.Add(scroll);
        var bottom = new DockPanel { Margin = new Thickness(0, 17, 0, 0) }; bottom.Children.Add(Theme.Label("Drag an item onto a category to organize it.", 12, Theme.Muted)); var files = Theme.Button(UiIcons.Label("Open Files", 12, "folder"), "Open Harbor Files", () => { Dismiss(false); shell.OpenFiles(); }); DockPanel.SetDock(files, System.Windows.Controls.Dock.Right); bottom.Children.Add(files); Grid.SetRow(bottom, 2); contents.Children.Add(bottom);
    }
    void CatalogChanged() { if (!closed && source == "All apps") Render(); }
    void Render()
    {
        using var viewport = ScrollPosition.Keep(sidebar);
        sidebar.Children.Clear();
        Link("Desktop", "home", () => Choose("Desktop", "All"), source == "Desktop" && category == "All", "Home");
        Link("Frequently used", "clock", () => Choose("Frequently used", "All"), source == "Frequently used");
        var divider = new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(10, 12, 12, 12) }; AutomationProperties.SetAutomationId(divider, "home-permanent-divider"); sidebar.Children.Add(divider);
        foreach (string tab in HomeLayout.OrderedTabs(shell.Config).Where(t => !shell.Config.HomeHiddenTabs.Contains(t)))
        {
            if (tab == "All apps") { Link(tab, "apps", () => Choose("All apps", "All"), source == "All apps" && category == "All"); continue; }
            string icon = tab switch { "All" => "grid", "Favorites" => "pin", "Games" => "game", "Social" => "profiles", "Music & video" => "volume", "Folders" => "folder", "Files" => "file", _ => "controls" };
            int count = DesktopLibraryModel.Query(Items, shell.Config, tab, "").Count;
            var button = Link(tab, icon, () => Choose(source == "Frequently used" ? "Desktop" : source, tab), category == tab && tab != "All" && source != "Frequently used", null, count);
            if (tab is "All" or "Favorites") continue;
            button.AllowDrop = true; button.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(ItemDrag) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            button.Drop += (_, e) => { if (e.Data.GetData(ItemDrag) is string path) { DesktopLibraryModel.Move(shell.Config, path, tab); Render(); } e.Handled = true; };
        }
        var add = Theme.Button(UiIcons.Label("New category", 13, "plus"), "Create a Home category", () => { string? name = NameDialog.Ask(this, "New category", ""); if (string.IsNullOrWhiteSpace(name) || new[] { "All", "Favorites", "Desktop", "All apps", "Frequently used" }.Contains(name, StringComparer.OrdinalIgnoreCase) || DesktopLibraryModel.Groups(shell.Config).Contains(name, StringComparer.OrdinalIgnoreCase)) return; shell.Config.DesktopCustomCategories.Add(name); shell.Config.Save(); category = name; source = "Desktop"; page = 0; Render(); }); add.HorizontalContentAlignment = HorizontalAlignment.Left; sidebar.Children.Add(add);
        RenderCards();
        void Choose(string next, string group) { source = next; category = group; page = 0; Render(); }
        Button Link(string key, string icon, Action action, bool selected, string? fallback = null, int? count = null)
        {
            string name = shell.Config.HomeTabNames.GetValueOrDefault(key, fallback ?? HomeLayout.Label(shell.Config, key));
            var button = Theme.Button(UiIcons.Label(name + (shell.Config.HomeShowCounts && count.HasValue ? "   " + count : ""), 13, icon), name, action); button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Background = selected ? Theme.Raised : Brushes.Transparent; button.Foreground = selected ? Theme.Accent : Theme.Text; button.Padding = new Thickness(10); button.Margin = new Thickness(0, 0, 3, 4); sidebar.Children.Add(button);
            var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Rename tab", () => { var name = NameDialog.Ask(this, "Rename tab", HomeLayout.Label(shell.Config, key)); if (string.IsNullOrWhiteSpace(name)) return; shell.Config.HomeTabNames[key] = name; shell.Config.Save(); Render(); }));
            if (key is not "Desktop" and not "Frequently used") menu.Items.Add(Theme.Item("Hide tab", () => { shell.Config.HomeHiddenTabs.Add(key); shell.Config.Save(); Render(); }));
            menu.Items.Add(Theme.Item("Customize Home", () => { Dismiss(false); shell.OpenHomeSettings(); })); button.ContextMenu = menu; return button;
        }
    }
    void RenderCards()
    {
        cacheDirty = true;
        cards.Children.Clear(); cards.RowDefinitions.Clear(); cards.ColumnDefinitions.Clear();
        var matches = DesktopLibraryModel.Query(Items, shell.Config, category, search.Text); if (source == "Frequently used") matches = HomeLayout.Frequent(shell.Config, matches); int columns = Math.Min(shell.Config.HomeColumns, Math.Max(2, (int)((panel.Width - shell.Config.HomeSidebarWidth - 74) / 125))), pageSize = columns * shell.Config.HomeRows;
        page = Math.Clamp(page, 0, Math.Max(0, (matches.Count - 1) / pageSize));
        heading.Text = category == "All" ? HomeLayout.Label(shell.Config, source) : HomeLayout.Label(shell.Config, category);
        summary.Text = source == "Frequently used" ? shell.Config.AppUsage.Count == 0 ? "Your pinned apps first. This list learns as you open apps through Harbor." : "Your most-used apps, ranked by how often you open them." : matches.Count + (matches.Count == 1 ? " item" : " items") + (source == "Desktop" ? " from your desktop" : " from your installed apps");
        footer.Text = "Page " + (page + 1) + " of " + Math.Max(1, (int)Math.Ceiling(matches.Count / (double)pageSize));
        for (int col = 0; col < columns; col++) cards.ColumnDefinitions.Add(new ColumnDefinition());
        int index = 0;
        foreach (var item in matches.Skip(page * pageSize).Take(pageSize))
        {
            int row = index / columns; if (cards.RowDefinitions.Count <= row) cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var body = new StackPanel { Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Stretch };
            var icon = item.Folder ? FileIconConverter.FolderArt(item.Name, shell.Config.HomeIconSize) : Theme.AppIcon(item.Path, item.Name, shell.Config.HomeIconSize); icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new Thickness(0, 0, 0, 15); body.Children.Add(icon);
            var title = Theme.Label(item.Name, shell.Config.HomeTextSize); title.FontWeight = FontWeights.SemiBold; title.TextTrimming = TextTrimming.CharacterEllipsis; title.MaxWidth = 160; title.HorizontalAlignment = HorizontalAlignment.Left; title.Visibility = shell.Config.HomeShowLabels ? Visibility.Visible : Visibility.Collapsed; body.Children.Add(title);
            var kind = Theme.Label(item.Kind, 11, Theme.Muted); kind.Margin = new Thickness(0, 6, 0, 0); kind.Visibility = shell.Config.HomeShowKinds ? Visibility.Visible : Visibility.Collapsed; body.Children.Add(kind);
            var tile = Theme.Button(body, "Open " + item.Name, () => { if (AppDrag.Suppressed) return; Dismiss(false); shell.Launch(item.Path); }); tile.Background = Theme.Raised; tile.Padding = new Thickness(16); tile.HorizontalContentAlignment = HorizontalAlignment.Stretch; tile.Margin = new Thickness(0, 0, 12, 12); tile.MinHeight = Math.Max(100, shell.Config.HomeIconSize + shell.Config.HomeTextSize * 2 + 58);
            Grid.SetRow(tile, row); Grid.SetColumn(tile, index % columns); cards.Children.Add(tile); index++;
            var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Open", () => { Dismiss(false); shell.Launch(item.Path); })); menu.Items.Add(Theme.Item(shell.Config.DesktopFavorites.Contains(item.Path) ? "Remove favorite" : "Add favorite", () => { if (!shell.Config.DesktopFavorites.Remove(item.Path)) shell.Config.DesktopFavorites.Add(item.Path); shell.Config.Save(); Render(); }));
            var groups = new MenuItem { Header = "Move to category", Icon = UiIcons.Icon("folder") }; foreach (string group in DesktopLibraryModel.Groups(shell.Config)) groups.Items.Add(Theme.Item(group, () => { DesktopLibraryModel.Move(shell.Config, item.Path, group); Render(); })); menu.Items.Add(groups);
            if (item.App) menu.Items.Add(Theme.Item("Pin to taskbar", () => shell.Pin(item.Pin))); menu.Items.Add(Theme.Item("Show in Files", () => { Dismiss(false); shell.OpenFiles(item.Folder ? item.Path : Path.GetDirectoryName(item.Path)); })); tile.ContextMenu = menu;
            AppDrag.BindData(tile, () => { var data = TaskbarFolders.Data(item.Pin); data.SetData(ItemDrag, item.Path); return data; });
        }
        if (matches.Count == 0) { var empty = new StackPanel { Margin = new Thickness(25) }; empty.Children.Add(UiIcons.Icon("search", 32)); empty.Children.Add(Theme.Label(search.Text.Length > 0 ? "Nothing matches this search" : "Room for your next idea", 20)); var help = new TextBlock { Text = source == "Desktop" ? "Your desktop shortcuts appear here. Try All apps, or drag an item onto this category." : "Apps appear here as Harbor discovers them. Try another category or search.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 13, Margin = new Thickness(0, 10, 0, 0) }; empty.Children.Add(help); Grid.SetColumnSpan(empty, columns); cards.Children.Add(empty); }
        Motion.Enter(cards, 0); QueueSnapshot();
    }
    void ArrangeReveal()
    {
        if (frame == null) return;
        double p = Math.Clamp(Reveal, 0, 1), w = panel.Width, h = panel.Height;
        double panelLeft = (Width - w) / 2, panelTop = (Height - h) / 2;
        if (Theme.Dragon && !closing) { ArrangeDragon(p, w, h, panelLeft, panelTop); if (inMotion) frames.Frame(); return; }
        dragonImage.Visibility = Visibility.Hidden; dragonFire.Progress = 0; dragonCurtain.Visibility = Visibility.Hidden; layers.Visibility = header.Visibility = Visibility.Visible; layers.OpacityMask = null; layers.Clip = viewport; header.Clip = null; frame.Visibility = Visibility.Visible;
        double flyingSize = originSize * (1 - p) + 48 * p;
        var logoCenter = EmblemState.Travel(origin, new Point(panelLeft + 50, panelTop + 50), p);
        double surfaceWidth = originSize + 52 + (w - originSize - 52) * p, surfaceHeight = originSize + 52 + (h - originSize - 52) * p;
        frame.Width = surfaceWidth; frame.Height = surfaceHeight;
        offset.X = logoCenter.X - flyingSize / 2 - 26; offset.Y = logoCenter.Y - flyingSize / 2 - 26;
        contentOffset.X = offset.X; contentOffset.Y = offset.Y + flyingSize - 48;
        viewport.Rect = new Rect(offset.X - contentOffset.X, offset.Y - contentOffset.Y, surfaceWidth, surfaceHeight);
        contents.Opacity = snapshot.Opacity = Smooth((p - .15) / .70); bodyOffset.Y = 0; frameBrush.Opacity = frameEdge.Opacity = Smooth(p / .34);
        flyScale.ScaleX = flyScale.ScaleY = flyingSize / originSize; flying.Turn = EmblemState.MorphTurn(originTurn, p); flying.Opacity = 1;
        flyOffset.X = logoCenter.X - flyingSize / 2; flyOffset.Y = logoCenter.Y - flyingSize / 2;
        header.Width = Math.Max(1, surfaceWidth - 52); header.Height = flyingSize; headerEmblem.Width = headerEmblem.Height = flyingSize;
        Canvas.SetLeft(header, offset.X + 26); Canvas.SetTop(header, offset.Y + 26); header.Opacity = Smooth((p - .20) / .40);
        if (inMotion) frames.Frame();
    }
    void ArrangeDragon(double p, double w, double h, double panelLeft, double panelTop)
    {
        // Stable layout throughout this performance. Running the ordinary card
        // morph first invalidates the entire hidden library twice per frame.
        frame.Visibility = p < 1 ? Visibility.Hidden : Visibility.Visible; frame.Width = w; frame.Height = h; offset.X = panelLeft; offset.Y = panelTop; frameBrush.Opacity = frameEdge.Opacity = 1;
        contentOffset.X = panelLeft; contentOffset.Y = panelTop; bodyOffset.Y = 0; double shown = p;
        if (dragonFrames != null)
        {
            var sample = dragonFrames.At(p); shown = sample.Progress; dragonFire.Visibility = Visibility.Hidden;
            dragonImage.Visibility = sample.Image != null && p < 1 ? Visibility.Visible : Visibility.Hidden; dragonImage.Source = sample.Image;
            if (!sample.Bounds.IsEmpty) { dragonImage.Width = sample.Bounds.Width; dragonImage.Height = sample.Bounds.Height; Canvas.SetLeft(dragonImage, dragonFire.RenderOffset.X + sample.Bounds.X); Canvas.SetTop(dragonImage, dragonFire.RenderOffset.Y + sample.Bounds.Y); }
            layers.Clip = null; layers.OpacityMask = null;
        }
        else { dragonFire.Progress = p; dragonFire.Visibility = Visibility.Visible; layers.Clip = dragonFire.ContentClip(new Point(panelLeft, panelTop)); layers.OpacityMask = null; }
        double content = DragonHomeFire.ContentReveal(shown);
        layers.Visibility = header.Visibility = content > 0 ? Visibility.Visible : Visibility.Hidden;
        contents.Opacity = snapshot.Opacity = header.Opacity = 1;
        header.Width = w - 52; header.Height = headerEmblem.Width = headerEmblem.Height = 48; Canvas.SetLeft(header, panelLeft + 26); Canvas.SetTop(header, panelTop + 26); header.Clip = null;
        dragonCurtain.Width = w; dragonCurtain.Height = h; Canvas.SetLeft(dragonCurtain, panelLeft); Canvas.SetTop(dragonCurtain, panelTop); dragonCurtainBrush.Color = Theme.Panel.Color; dragonCurtainBrush.Opacity = 1 - content; dragonCurtain.Visibility = content > 0 && content < 1 ? Visibility.Visible : Visibility.Hidden;
        if (p < .2) { flyOffset.X = origin.X - originSize / 2; flyOffset.Y = origin.Y - originSize / 2; flyScale.ScaleX = flyScale.ScaleY = 1; flying.Turn = originTurn; }
        else { flyScale.ScaleX = flyScale.ScaleY = 48 / originSize; flying.Turn = EmblemState.MorphTurn(originTurn, 1); flyOffset.X = panelLeft + 26; flyOffset.Y = panelTop + 26; }
        flying.Opacity = p < .2 ? DragonHomeFire.IdleOpacity(p) : content > 0 ? 1 : 0;
    }
    static double Smooth(double value) { double p = Math.Clamp(value, 0, 1); return p * p * (3 - 2 * p); }
    void CapturePanel()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        panel.Visibility = Visibility.Visible; contents.Opacity = 1; bodyOffset.Y = 0;
        panel.Measure(new Size(panel.Width, panel.Height)); panel.Arrange(new Rect(0, 0, panel.Width, panel.Height)); panel.UpdateLayout();
        double dpi = Native.Scale(monitor); var image = new RenderTargetBitmap((int)Math.Ceiling(panel.Width * dpi), (int)Math.Ceiling(panel.Height * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32); image.Render(panel); image.Freeze(); savedPanel = image; snapshot.Source = image; cacheDirty = false;
        SnapshotMilliseconds = watch.Elapsed.TotalMilliseconds;
    }
    internal void PrepareOpening()
    {
        if (shell.Config.DockMotionMilliseconds == 0) return;
        if (!Prepared) CapturePanel();
        if (!demo && Theme.Dragon) { try { dragonFrames = DragonFrameCache.Get(dragonFire).GetAwaiter().GetResult(); } catch (Exception ex) { Log.Write("Dragon frame preparation: " + ex.Message); } }
        snapshot.Source = savedPanel; snapshot.Visibility = Visibility.Visible; panel.Visibility = Visibility.Hidden; UsesCachedMorph = true; ArrangeReveal();
    }
    async void QueueSnapshot()
    {
        if (demo || !IsLoaded || closed || closing || cacheScheduled) return;
        cacheScheduled = true;
        do { await Task.Delay(240); } while (!closed && !closing && cards.HasAnimatedProperties);
        cacheScheduled = false; if (closed || closing || inMotion) return; CapturePanel();
    }
    void Animate(bool open)
    {
        if (demo || shell.Config.DockMotionMilliseconds == 0) { Reveal = open ? 1 : 0; if (!open) Close(); else FinishOpening(); return; }
        if (savedPanel == null || cacheDirty) CapturePanel();
        snapshot.Source = savedPanel; snapshot.Visibility = Visibility.Visible; panel.Visibility = Visibility.Hidden; UsesCachedMorph = true;
        inMotion = true;
        double current = Reveal, target = open ? 1 : 0; BeginAnimation(RevealProperty, null); Reveal = current;
        if (open) animationStartReveal = current;
        var animation = Motion.Animation(current, target, open ? Theme.Dragon ? 1250 : 740 : 420); animation.FillBehavior = FillBehavior.HoldEnd;
        animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
        animation.Completed += (_, _) => { if (closed) return; Reveal = target; BeginAnimation(RevealProperty, null); inMotion = false; if (!open) Close(); else FinishOpening(); }; BeginAnimation(RevealProperty, animation);
    }
    void FinishOpening()
    {
        panel.Visibility = Visibility.Visible; snapshot.Visibility = Visibility.Hidden; ArrangeReveal(); search.Focus();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { if (closed || closing || !IsVisible) return; settled = true; Opened?.Invoke(); QueueSnapshot(); }));
    }
    internal void Dismiss(bool animated = true)
    {
        if (closed) return;
        if (!animated) { closing = true; IsHitTestVisible = false; BeginAnimation(RevealProperty, null); Close(); return; }
        if (closing) return; closing = true; IsHitTestVisible = false; Animate(false);
    }
    internal void PreparePreview(double reveal = 1, string group = "All")
    {
        if (category != group) { category = group; Render(); }
        BeginAnimation(RevealProperty, null);
        if (reveal > 0 && reveal < 1) { if (savedPanel == null || cacheDirty) CapturePanel(); snapshot.Source = savedPanel; snapshot.Visibility = Visibility.Visible; panel.Visibility = Visibility.Hidden; UsesCachedMorph = true; }
        else { panel.Visibility = Visibility.Visible; snapshot.Visibility = Visibility.Hidden; }
        Reveal = reveal; ArrangeReveal();
    }
    internal static List<DesktopLibraryItem> DemoItems() => new() {
        new("Orbit", @"C:\Apps\Orbit.exe", "Orbit.exe"), new("Steam", @"C:\Apps\Steam.exe", "Steam.exe"), new("Rocket League", @"C:\Apps\RocketLeague.exe", "RocketLeague.exe"), new("Discord", @"C:\Apps\Discord.exe", "Discord.exe"), new("Spotify", @"C:\Apps\Spotify.exe", "Spotify.exe"), new("Blender", @"C:\Apps\blender.exe", "blender.exe"), new("Visual Studio Code", @"C:\Apps\Code.exe", "Code.exe"), new("Projects", @"C:\Desktop\Projects", @"C:\Desktop\Projects", true), new("Pictures", @"C:\Desktop\Pictures", @"C:\Desktop\Pictures", true), new("Notes", @"C:\Desktop\Notes.txt", @"C:\Desktop\Notes.txt"), new("Medal", @"C:\Apps\Medal.exe", "Medal.exe"), new("Files", @"C:\Apps\Files.exe", "Files.exe")
    };
}
