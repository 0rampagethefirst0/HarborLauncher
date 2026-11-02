using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Harbor;

// Desktop effects are staged examples; widgets below use the actual Widget renderer.
// This scene never launches apps, alters windows, changes volume, or saves fields.
internal sealed partial class ModDemoScene
{
    internal const int Width = 720, Height = 400, Frames = 288, Columns = 12, FilmWidth = 540, FilmHeight = 300;
    internal const double FrameMilliseconds = 1000d / 90;
    readonly string originalId;
    readonly Shell shell;
    readonly ModDefinition original;
    readonly ModDefinition mod;
    readonly WidgetDemoData data = new();
    readonly Widget? widget;
    Viewbox? widgetHost;
    Viewbox? homeHost;
    readonly HarborHome? homeDemo;
    readonly FrameworkElement? homeView;
    readonly Canvas canvas = new() { Width = Width, Height = Height, ClipToBounds = true, Background = Theme.Brush("#101722") };
    readonly Brush accent;
    internal FrameworkElement View => canvas;
    internal string Instruction { get; private set; } = "";
    internal string Renderer => mod.Kind == "theme" ? "shell-palette" : mod.Kind == "widget" ? "native-widget" : mod.Component;
    internal ModDemoScene(Shell shell, ModDefinition source)
    {
        originalId = source.Id; original = source; this.shell = shell;
        mod = JsonSerializer.Deserialize<ModDefinition>(JsonSerializer.Serialize(source, Mods.Json), Mods.Json)!;
        mod.Id = "preview." + Guid.NewGuid().ToString("N");
        accent = ModBanner.Accent(ModTaxonomy.MainOf(mod));
        if (mod.Component == "desktop-icons") { homeDemo = new HarborHome(shell, shell.CurrentMonitor(), demo: true); homeView = (FrameworkElement)homeDemo.Content; homeDemo.Content = null; homeDemo.Close(); }
        if (mod.Kind == "widget")
        {
            Option("width", mod.Component == "dashboard" ? "360" : "310");
            Option("paths", "C:\\Projects\nC:\\Downloads\nC:\\Documents");
            Option("apps", "Calculator | calc.exe\nNotes | notepad.exe\nBrowser | https://example.com");
            Option("items", "Finish the icon set\nReview the new desktop\nTake a break");
            widget = new Widget(shell, mod, true, data, true);
        }
        Draw(.72);
    }
    void Option(string key, string value) { var option = mod.Options.FirstOrDefault(o => o.Key == key); if (option != null) option.Default = value; else mod.Values[key] = value; }
    internal void Draw(double phase)
    {
        data.Phase = Math.Clamp(phase, 0, 1); if (widgetHost != null) { widgetHost.Child = null; widgetHost = null; } if (homeHost != null) { homeHost.Child = null; homeHost = null; } canvas.Children.Clear();
        if (mod.Kind == "theme") Palette();
        else if (mod.Kind == "widget") WidgetScene();
        else if (mod.Kind == "feature") FeatureScene();
        else DesktopEffect();
        Instruction = ModCopy.Caption(originalId) ?? Instruction;
        Text("SAMPLE DATA · PREVIEW", 20, 378, 10, "#8CA2BD");
        Text(Instruction, 20, 350, 15, "#DCE8F8");
    }
    void WidgetScene()
    {
        double p = data.Phase;
        double expand = p < .18 ? 0 : p < .40 ? Smooth((p - .18) / .22) : p < .88 ? 1 : 1 - Smooth((p - .88) / .12);
        string key = mod.Component == "dashboard" ? OriginalDashboard() : mod.Component;
        Bar();
        AppWindow(new Rect(28, 54, 650, 266), key == "folders" && p > .52 ? "Harbor Files · Projects" : "Your workspace", 1, true);
        if (key == "folders" && p > .52) { FileRow(54, 115, "Designs", true); FileRow(54, 155, "Harbor", true); FileRow(54, 195, "Sketch.txt", false); }
        else if (key is "layouts" or "window-desk") { double progress = Smooth(Math.Clamp((p - .36) / .3, 0, 1)); AppWindow(new Rect(42, 95, 228, 102 + (1 - progress) * 74), "Browser", 2, true); AppWindow(new Rect(84 - 42 * progress, 138 + 66 * progress, 228, 102), "Notes", 0, false); }
        else { Text("Keep working here", 54, 115, 21, "#B3C5DD"); Text("Tools expand only when you need them.", 54, 150, 13, "#879FBF"); }
        double controlWidth = key is "system" or "resource-desk" ? 192 : key == "storage" ? 224 : 112;
        var controlPosition = PluginSnapping.Place(new Rect(25, 58, 680, 262), new Size(controlWidth, 36), PluginSnapping.Default(original), .9, .5);
        var control = PluginHandle.Face(shell, original, controlWidth, () => { }, out _, out _, true); control.Width = controlWidth; control.Height = 36; At(control, controlPosition.Left, controlPosition.Top); canvas.Children.Add(control);
        if (expand > .001)
        {
            double x = (controlPosition.Left < 360 ? controlPosition.Right + 12 : controlPosition.Left - 318) + (1 - expand) * 12; x = Math.Clamp(x, 24, 390);
            widget!.Tick(); widget.View.Measure(new Size(widget.View.Width, double.PositiveInfinity)); double bodyHeight = Math.Min(207, widget.View.DesiredSize.Height * 282 / widget.View.Width); double panelHeight = bodyHeight + 68, top = Math.Max(52, 171 - panelHeight / 2);
            var panel = new Border { Width = 306, Height = panelHeight, Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Opacity = expand }; At(panel, x, top); canvas.Children.Add(panel);
            Text(original.Name, x + 16, top + 12, 16); Icon("close", x + 276, top + 15, 13);
            PutWidget(x + 12, top + 40, 282, bodyHeight); widgetHost!.Opacity = expand;
            Text("Click away to collapse", x + 16, top + panelHeight - 18, 10, "#9AA8BC");
        }
        Pointer(p < .3 ? 660 : 407, p < .3 ? 174 : key is "calculator" or "notes" ? 140 : 204, p is > .16 and < .25);
        Instruction = ModCopy.Caption(originalId) ?? "Expand the control when you need the full tool.";
    }
    string OriginalDashboard()
    {
        string text = mod.Name.ToLowerInvariant();
        return text.Contains("sound") ? "sound-desk" : text.Contains("resource") ? "resource-desk" : text.Contains("day planner") ? "day-planner" : text.Contains("window desk") ? "window-desk" : "custom";
    }
    void PutWidget(double x, double y, double width, double height)
    {
        widget!.Tick();
        widget.View.Measure(new Size(widget.View.Width, double.PositiveInfinity)); double naturalHeight = widget.View.DesiredSize.Height * width / widget.View.Width;
        var box = new Viewbox { Width = width, Height = naturalHeight, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, VerticalAlignment = VerticalAlignment.Top, Child = widget.View };
        if (naturalHeight > height) box.RenderTransform = new TranslateTransform(0, -(naturalHeight - height) * Smooth(Math.Clamp((data.Phase - .40) / .40, 0, 1)));
        widgetHost = box;
        var viewport = new Border { Width = width, Height = height, ClipToBounds = true, Child = box }; At(viewport, x, y); canvas.Children.Add(viewport);
    }
    void DesktopEffect()
    {
        double p = data.Phase, move = Smooth(Math.Clamp((p - .16) / .5, 0, 1)); Bar();
        switch (mod.Component)
        {
            case "window-topmost":
                AppWindow(new Rect(60, 72, 315, 204), "Notepad · pinned", 0, true);
                if (p > .25) AppWindow(new Rect(259, 108, 404, 210), "Browser · focused", 1, false);
                AppWindow(new Rect(60, 72, 315, 204), "Notepad · pinned", 0, true); Icon("pin", 337, 78, 17); Pointer(591, 136, p > .4);
                Instruction = "Focus another app. The pinned Notepad window stays above it."; break;
            case "window-opacity":
                AppWindow(new Rect(54, 71, 465, 244), "Files underneath", 3, false);
                FileRow(89, 128, "Designs", true); FileRow(89, 171, "Harbor", true); FileRow(89, 214, "Sketch.txt", false);
                FileRow(312, 209, "Samples", true); FileRow(312, 250, "Demo.txt", false);
                var overlay = AppWindow(new Rect(255, 87, 402, 218), "Notepad", 0, true); overlay.Opacity = 1 - .45 * move;
                KeyBadge("Opacity  " + (100 - 45 * move).ToString("0") + "%", 479, 317);
                Instruction = "Example: lower opacity to 55% and see the files behind the window."; break;
            case "window-placement":
                var target = Box(367, 65, 330, 260, "#192B35", 8); target.BorderBrush = accent; target.BorderThickness = new Thickness(2);
                Rect from = new(88, 113, 292, 177), to = new(369, 67, 326, 256);
                AppWindow(Lerp(from, to, move), "Notepad", 0, true); Pointer(335 + 300 * move, 141 - 50 * move, false);
                Instruction = "Example region: Right. Launch Notepad directly into that screen region."; break;
            case "window-memory":
                var remembered = Box(360, 67, 315, 249, "#162831", 7); remembered.BorderBrush = accent; remembered.BorderThickness = new Thickness(1);
                if (p < .43) AppWindow(Lerp(new Rect(55, 96, 298, 205), new Rect(360, 67, 315, 249), Smooth(Math.Clamp(p / .35, 0, 1))), "Notepad", 0, true);
                if (p > .58) AppWindow(new Rect(360, 67, 315, 249), "Notepad · reopened", 0, true);
                KeyBadge(p < .43 ? "1  Move" : p < .58 ? "2  Close" : "3  Reopen", 30, 309);
                Instruction = "Move the window, close it, then reopen it in the saved position."; break;
            case "dock-scroll-windows":
                int active = Math.Clamp((int)(p * 3), 0, 2);
                for (int i = 0; i < 3; i++) AppWindow(new Rect(58 + i * 31, 65 + i * 18, 499, 203), "Browser · " + new[] { "Projects", "Research", "Downloads" }[i], i, false);
                AppWindow(new Rect(121, 99, 499, 203), "Browser · " + new[] { "Projects", "Research", "Downloads" }[active], active, true);
                Dock(false, "Browser", (active + 1) + " / 3"); MouseWheel(355, 290); Instruction = "Scroll over one dock icon to cycle that app’s three windows."; break;
            case "dock-middle-close":
                AppWindow(new Rect(94, 77, 434, 211), "Notepad · first window", 0, false);
                if (p < .48) AppWindow(new Rect(195, 104, 434, 200), "Notepad · second window", 2, true);
                Dock(false, "Notepad", p < .48 ? "2 windows" : "1 window"); MouseWheel(357, 290, true); Instruction = "Middle-click the dock icon. Its front window closes; the other remains."; break;
            case "dock-monitor-only":
                for (int i = 0; i < 2; i++)
                {
                    double x = 23 + i * 350; var display = Box(x, 61, 324, 260, "#151F2E", 9); display.BorderBrush = Theme.Line; display.BorderThickness = new Thickness(2);
                    Text("DISPLAY " + (i + 1), x + 14, 72, 11, "#99AEC9"); AppWindow(new Rect(x + 20, 105, 282, 141), i == 0 ? "Notepad" : "Browser", i, true);
                    Box(x + 76, 265, 170, 39, "#27374F", 8); AppIcon("Notes", x + 90, 272, 26); AppIcon("Browser", x + 177, 272, 26);
                    if (p < .3 || i == 0) Box(x + 99, 300, 9, 3, "#83DFD0", 1);
                    if (p < .3 || i == 1) Box(x + 185, 300, 9, 3, "#83DFD0", 1);
                }
                Instruction = "Each dock shows running indicators only for windows on its own display."; break;
            case "dock-labels":
                AppWindow(new Rect(111, 76, 496, 198), "Notes · planning", 0, true); Dock(p > .28); Pointer(399, 303, false);
                Instruction = "App names appear beside the dock icons, rather than icons alone."; break;
            case "dock-wheel-volume":
                AppWindow(new Rect(39, 71, 305, 219), "Music", 5, false); AppWindow(new Rect(374, 71, 305, 219), "Game", 5, false);
                Text("Music volume", 62, 123, 19); Meter(63, 164, 244, 70 - move * 38);
                Text("Game volume", 397, 123, 19); Meter(398, 164, 244, 70);
                Dock(false, "Music"); KeyBadge("Alt", 238, 306); MouseWheel(357, 288);
                Instruction = "Alt + scroll changes Music’s volume. Game volume stays at 70%."; break;
            case "toolbar-wheel-volume":
                AppWindow(new Rect(116, 81, 489, 234), "Your app", 1, true);
                Box(493, 23, 198, 26, "#30435D", 5); Icon("volume", 505, 28, 16); Text("System  " + (38 + 30 * move).ToString("0") + "%", 533, 27, 14);
                MouseWheel(599, 40); Instruction = "Scroll on the top bar to change the system volume."; break;
            case "clock-seconds":
                AppWindow(new Rect(120, 94, 480, 222), "Your app", 0, true);
                Box(355, 19, 346, 34, "#2A3B52", 5); Text(p < .3 ? "Thu, Oct 29   08:24" : data.Now.AddHours(-4).ToString("ddd, MMM d   HH:mm:ss"), 371, 24, 18, "#83DFD0");
                KeyBadge("ddd, MMM d   HH:mm:ss", 252, 281); Instruction = "Add seconds and your chosen date/time format to the real toolbar clock."; break;
            case "title-actions":
                Box(207, 20, 275, 32, "#30445B", 4); Text("Notepad · project notes", 221, 25, 16);
                AppWindow(Lerp(new Rect(177, 111, 371, 192), new Rect(20, 57, 680, 278), move), "Notepad", 0, true);
                Pointer(366, 34, true); if (p > .67) Menu(335, 66);
                Instruction = "Double-click the bar title to maximize. Right-click it for window actions."; break;
            default: Instruction = "A native Harbor capability. Supply a preview when sharing a custom plugin."; break;
        }
    }
    void Palette()
    {
        if (originalId == "harbor.tokyo-dragon")
        {
            var background = new System.Windows.Controls.Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Harbor;component/Assets/tokyo-dragon-outline.png")), Width = Width, Height = 340, Stretch = Stretch.UniformToFill }; canvas.Children.Add(background);
            double opening = Smooth(Math.Clamp((data.Phase - .18) / .72, 0, 1));
            var actor = new DragonHomeFire { Width = 720, Height = 340, Origin = new Point(350, 180), LogoSize = 140, Progress = opening, HomeBounds = new Rect(230, 58, 450, 252), LogoTint = (Color)ColorConverter.ConvertFromString("#F33D4F"), FireTint = (Color)ColorConverter.ConvertFromString("#FF6A20"), PanelTint = (Color)ColorConverter.ConvertFromString("#171216") }; canvas.Children.Add(actor);
            var logo = new DrawingVisual(); using (var dc = logo.RenderOpen()) DragonArt.Draw(dc, 180, 180, -16 + data.Phase * 90, tint: (Color)ColorConverter.ConvertFromString("#F33D4F"));
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(180, 180, 96, 96, PixelFormats.Pbgra32); bitmap.Render(logo); bitmap.Freeze();
            var mark = new System.Windows.Controls.Image { Source = bitmap, Width = 130, Height = 130, Opacity = DragonHomeFire.IdleOpacity(opening) }; At(mark, 285, 115); canvas.Children.Add(mark);
            var home = new Canvas { Width = 720, Height = 340, Clip = actor.Surface, Opacity = DragonHomeFire.ContentReveal(opening) }; var previousTarget = target; target = home;
            Text("Harbor Home", 255, 78, 19, "#F7ECEE");
            string[] names = { "Files", "Browser", "Music", "Games", "Discord", "Notes", "Photos", "Tools" }; for (int i = 0; i < names.Length; i++) { double x = 253 + i % 4 * 103, y = 123 + i / 4 * 71; Box(x, y, 91, 59, "#2A1B22", 8); Text(names[i], x + 10, y + 21, 12, "#F7ECEE"); }
            target = previousTarget; canvas.Children.Add(home);
            Instruction = "The dragon leans upward at 17 degrees. Its fire changes shape into Home."; return;
        }

        string Color(string key, string fallback) => mod.Options.FirstOrDefault(o => o.Key == key)?.Default ?? mod.Values.GetValueOrDefault(key, fallback);
        double split = 1 - Smooth(Math.Clamp((data.Phase - .1) / .7, 0, 1));
        ShellPalette("#141922", "#1D2430", "#8AB4FF", "#EDF1FA");
        var after = new Canvas { Width = 720, Height = 340, Clip = new RectangleGeometry(new Rect(split * 720, 0, (1 - split) * 720, 340)) };
        var previous = target; target = after;
        ShellPalette(Color("background", "#141922"), Color("panel", "#1D2430"), Color("accent", "#8AB4FF"), Color("text", "#EDF1FA")); target = previous; canvas.Children.Add(after);
        if (split is > .02 and < .98) { var line = Box(split * 720 - 1, 8, 2, 326, "#E4EEFC", 0); }
        Instruction = "Before → " + mod.Name + ": the bar, dock, menus and widgets use the new palette.";
    }
    Canvas? target;
    Canvas Target => target ?? canvas;
    void ShellPalette(string background, string panel, string color, string text)
    {
        Box(0, 0, 720, 340, background, 0); var mark = new HarborEmblem(220) { Turn = -16 + data.Phase * 100, Tint = (Color)ColorConverter.ConvertFromString(color) }; At(mark, 425, 65); Target.Children.Add(mark); Box(15, 18, 690, 32, panel, 5);
        Text("Apps   Files   Mods", 30, 25, 13, text); Text("Thu, Oct 29     08:25", 536, 25, 12, text);
        Box(39, 73, 307, 231, panel, 11); Text("Apps", 58, 88, 22, text);
        for (int i = 0; i < 6; i++) { double x = 63 + i % 3 * 85, y = 136 + i / 3 * 77; var icon = UiIcons.Icon(new[] { "apps", "folder", "mods", "window", "profiles", "controls" }[i], 28); icon.SetValue(Control.ForegroundProperty, Theme.Brush(color)); At(icon, x + 10, y); Target.Children.Add(icon); Text(new[] { "Apps", "Files", "Mods", "Windows", "Profiles", "Settings" }[i], x, y + 39, 12, text); }
        Text("Your desktop, in your colors", 417, 250, 12, text);
        Box(388, 275, 294, 51, panel, 11); for (int i = 0; i < 5; i++) { Box(403 + i * 54, 286, 30, 29, color, 7); var icon = UiIcons.Icon(i % 2 == 0 ? "apps" : "folder", 18); At(icon, 409 + i * 54, 291); Target.Children.Add(icon); }
    }
    void Bar() { Box(15, 19, 690, 32, "#243248", 6); Text("Apps   Files   Mods", 28, 26, 13, "#D4E3F7"); Text("08:25", 648, 26, 13); }
    Canvas AppWindow(Rect bounds, string title, int kind, bool active)
    {
        var group = new Canvas { Width = bounds.Width, Height = bounds.Height, ClipToBounds = true }; At(group, bounds.X, bounds.Y); Target.Children.Add(group);
        Canvas? old = target; target = group;
        var frame = Box(0, 0, bounds.Width, bounds.Height, kind == 0 ? "#263044" : kind == 1 ? "#202D43" : "#283747", 8); frame.BorderBrush = active ? accent : Theme.Brush("#526580"); frame.BorderThickness = new Thickness(active ? 2 : 1);
        Box(2, 2, bounds.Width - 4, 29, active ? "#3B526B" : "#334157", 5); Text(title, 12, 8, 12); Text("−  □  ×", Math.Max(90, bounds.Width - 67), 6, 13, "#A9BED9");
        if (kind == 0) { Text("Project notes", 18, 48, 20); Text("A desktop that works your way.\nKeep this note close while you work.", 19, 85, 13, "#B8CAE1"); }
        else if (kind == 1) { Box(17, 47, bounds.Width - 34, 25, "#35455F", 5); Text("harbor.example / ideas", 26, 52, 11, "#B6CDEB"); for (int i = 0; i < 3; i++) Box(18 + i * (bounds.Width - 40) / 3, 91, (bounds.Width - 55) / 3, Math.Max(18, bounds.Height - 113), i == 1 ? "#425E73" : "#334A61", 6); }
        else if (kind == 2) { Text("Your project", 19, 48, 21); for (int i = 0; i < 3; i++) Box(20, 86 + i * 22, Math.Max(30, bounds.Width - 65 - i * 22), 5, "#6E8CAA", 2); }
        target = old; return group;
    }
    void Dock(bool labels = false, string active = "Notes", string badge = "")
    {
        double width = labels ? 495 : 216, x = (720 - width) / 2; Box(x, 279, width, 57, "#2D4058", 12);
        string[] apps = { "Notes", "Browser", "Files" };
        for (int i = 0; i < 3; i++) { double bx = x + 17 + i * (labels ? 163 : 66); AppIcon(apps[i], bx, 290, 31); if (labels) Text(apps[i], bx + 40, 297, 14); if (apps[i] == active || active == "Music" && i == 1) Box(bx + 9, 326, 13, 3, "#83DFD0", 1); }
        if (badge.Length > 0) KeyBadge(badge, 479, 307);
    }
    void AppIcon(string name, double x, double y, double size) { Box(x, y, size, size, name == "Browser" ? "#806CC6" : name == "Files" ? "#B48B44" : "#397D8E", 7); Icon(name == "Files" ? "folder" : name == "Browser" ? "window" : "notes", x + 6, y + 6, size - 12); }
    void FileRow(double x, double y, string name, bool folder) { Icon(folder ? "folder" : "window", x, y, 22); Text(name, x + 36, y + 2, 15); }
    void Meter(double x, double y, double width, double percent) { Box(x, y, width, 11, "#455775", 5); Box(x, y, Math.Max(1, width * percent / 100), 11, "#83DFD0", 5); Text(percent.ToString("0") + "%", x, y + 24, 29, "#CBEADF"); }
    void Menu(double x, double y) { Box(x, y, 211, 160, "#1D283A", 9); string[] lines = { "Minimize", "Close", "Always on top", "App mods" }; for (int i = 0; i < lines.Length; i++) { Icon(new[] { "minimize", "close", "pin", "mods" }[i], x + 12, y + 15 + i * 35, 16); Text(lines[i], x + 39, y + 13 + i * 35, 13); } }
    void KeyBadge(string text, double x, double y) { var b = Box(x, y, Math.Max(45, text.Length * 7.8 + 18), 27, "#38516C", 5); Text(text, x + 9, y + 5, 12, "#D6E6FA"); }
    void MouseWheel(double x, double y, bool middle = false) { var b = Box(x - 12, y, 31, 43, "#162232", 12); b.BorderBrush = accent; b.BorderThickness = new Thickness(2); Box(x, y + 5, 5, 14, middle ? "#FFC593" : "#83DFD0", 2); if (!middle) { Text("↑", x + 27, y - 5, 23, "#83DFD0"); Text("↓", x + 27, y + 18, 23, "#83DFD0"); } }
    void Pointer(double x, double y, bool click) { var path = new System.Windows.Shapes.Path { Data = Geometry.Parse("M0,0 L0,24 L6,18 L11,29 L16,27 L10,16 L20,16 Z"), Fill = Brushes.White, Stroke = Theme.Brush("#132438"), StrokeThickness = 1.4 }; At(path, x, y); Target.Children.Add(path); if (click) { var ring = new Ellipse { Width = 32, Height = 32, Stroke = accent, StrokeThickness = 3 }; At(ring, x - 9, y - 9); Target.Children.Add(ring); } }
    Border Box(double x, double y, double width, double height, string color, int radius) { var b = new Border { Width = width, Height = height, Background = Theme.Brush(color), CornerRadius = new CornerRadius(radius) }; At(b, x, y); Target.Children.Add(b); return b; }
    TextBlock Text(string text, double x, double y, double size, string color = "#EAF2FF") { var label = new TextBlock { Text = text, FontSize = size, Foreground = Theme.Brush(color), FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.NoWrap }; At(label, x, y); Target.Children.Add(label); return label; }
    void Icon(string kind, double x, double y, double size) { var icon = UiIcons.Icon(kind, size); At(icon, x, y); Target.Children.Add(icon); }
    static void At(UIElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); }
    static double Smooth(double p) => p * p * (3 - 2 * p);
    static Rect Lerp(Rect a, Rect b, double p) => new(a.X + (b.X - a.X) * p, a.Y + (b.Y - a.Y) * p, a.Width + (b.Width - a.Width) * p, a.Height + (b.Height - a.Height) * p);
}
