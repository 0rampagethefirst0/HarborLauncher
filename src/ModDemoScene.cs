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
internal sealed class ModDemoScene
{
    internal const int Width = 720, Height = 400, Frames = 192, Columns = 12, FilmWidth = 540, FilmHeight = 300;
    internal const double FrameMilliseconds = 1000d / 60;
    readonly string originalId;
    readonly ModDefinition mod;
    readonly WidgetDemoData data = new();
    readonly Widget? widget;
    Viewbox? widgetHost;
    readonly Canvas canvas = new() { Width = Width, Height = Height, ClipToBounds = true, Background = Theme.Brush("#101722") };
    readonly Brush accent;
    internal FrameworkElement View => canvas;
    internal string Instruction { get; private set; } = "";
    internal string Renderer => mod.Kind == "theme" ? "shell-palette" : mod.Kind == "widget" ? "native-widget" : mod.Component;
    internal ModDemoScene(Shell shell, ModDefinition source)
    {
        originalId = source.Id;
        mod = JsonSerializer.Deserialize<ModDefinition>(JsonSerializer.Serialize(source, Mods.Json), Mods.Json)!;
        mod.Id = "preview." + Guid.NewGuid().ToString("N");
        accent = ModBanner.Accent(ModTaxonomy.MainOf(mod));
        if (mod.Kind == "widget")
        {
            Option("width", mod.Component == "dashboard" ? "360" : "310");
            Option("paths", "C:\\Projects\nC:\\Downloads\nC:\\Documents");
            Option("apps", "Calculator | calc.exe\nNotes | notepad.exe\nBrowser | https://example.com");
            Option("items", "Finish the icon set\nReview the new desktop\nTake a break");
            widget = new Widget(shell, mod, true, data);
        }
        Draw(.72);
    }
    void Option(string key, string value) { var option = mod.Options.FirstOrDefault(o => o.Key == key); if (option != null) option.Default = value; else mod.Values[key] = value; }
    internal void Draw(double phase)
    {
        data.Phase = Math.Clamp(phase, 0, 1); if (widgetHost != null) { widgetHost.Child = null; widgetHost = null; } canvas.Children.Clear();
        if (mod.Kind == "theme") Palette();
        else if (mod.Kind == "widget") WidgetScene();
        else DesktopEffect();
        Instruction = ModCopy.Caption(originalId) ?? Instruction;
        Text("SAMPLE DATA · PREVIEW", 20, 378, 10, "#8CA2BD");
        Text(Instruction, 20, 350, 15, "#DCE8F8");
    }
    void WidgetScene()
    {
        double p = data.Phase;
        string key = mod.Component == "dashboard" ? OriginalDashboard() : mod.Component;
        Instruction = key switch {
            "clock" => "Your clock and date update on the desktop.",
            "world-clock" => "Local time 08:25 · Tokyo time 21:25, at the same moment.",
            "system" => "CPU history changes while RAM usage stays visible.",
            "network" => "Download and upload rates update separately.",
            "storage" => "Free and total capacity for each chosen drive.",
            "uptime" => "Time since the last Windows boot, down to the second.",
            "notes" => "Type directly on your desktop. Notes save locally.",
            "tasks" => "Check off individual tasks and keep your progress.",
            "focus" => "Start a countdown, pause it, or reset the session.",
            "stopwatch" => "Count up from zero, pause, and reset.",
            "calculator" => "Type an expression. The answer is calculated immediately.",
            "converter" => "Change units: 100 km/h becomes 62.137119 mph.",
            "folders" => "One click opens the chosen folder in Harbor Files.",
            "clipboard" => "Each copied text becomes another reusable clipboard item.",
            "layouts" => "Columns, Grid, Stack and Undo rearrange your windows.",
            "launchpad" => "Your own shortcuts open apps and websites.",
            "sound-desk" => "Change volume, mute, and control media in one panel.",
            "resource-desk" => "CPU, RAM, free space and uptime in a single dashboard.",
            "day-planner" => "A checklist, focus timer and editable notes together.",
            "window-desk" => "Open apps, files, alerts and window layouts from one desk.",
            _ => "Your own layout, rendered by Harbor’s native widget engine."
        };
        Bar();
        if (key is "layouts" or "window-desk")
        {
            int arrangement = p < .35 ? 0 : p < .7 ? 1 : 2;
            for (int i = 0; i < 3; i++)
            {
                Rect r = arrangement == 0 ? new Rect(28 + i * 92, 80, 88, 225) : arrangement == 1 ? new Rect(28 + i % 2 * 138, 80 + i / 2 * 114, 133, 109) : new Rect(28 + i * 26, 80 + i * 35, 220, 145);
                AppWindow(r, i == 0 ? "Notes" : i == 1 ? "Browser" : "Files", i, i == 2);
            }
            PutWidget(322, 64, 370, 260); Pointer(510, arrangement == 0 ? 125 : arrangement == 1 ? 165 : 205, true); return;
        }
        if (key is "folders" or "launchpad")
        {
            PutWidget(32, 76, 300, 239);
            if (p > .35)
            {
                AppWindow(new Rect(363, 69, 326, 245), key == "folders" ? "Harbor Files · Projects" : "Calculator", key == "folders" ? 3 : 4, true);
                if (key == "folders") { FileRow(387, 128, "Designs", true); FileRow(387, 168, "Harbor", true); FileRow(387, 208, "Sketch.txt", false); }
                else { Text("12 + 8", 396, 125, 23); Text("20", 396, 172, 50, "#83DFD0"); }
            }
            Pointer(126, 145, p is > .22 and < .5); return;
        }
        if (key == "world-clock") { Text("NEW YORK", 70, 125, 12, "#9BB2CE"); Text(p < .2 ? "08:24" : "08:25", 66, 149, 53); Text("Thu, Oct 29", 70, 215, 16, "#AFC1D9"); PutWidget(331, 81, 350, 215); return; }
        if (key == "day-planner")
        {
            PutWidget(25, 58, 300, 275);
            double top = 30 + Smooth(Math.Clamp((p - .3) / .6, 0, 1)) * 250;
            var zoom = new Rectangle { Width = 335, Height = 265, Fill = new VisualBrush(widget!.View) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, top, 360, 285), Stretch = Stretch.Uniform } }; At(zoom, 355, 66); canvas.Children.Add(zoom);
            Text("Full dashboard", 39, 321, 11, "#94AAC7"); Text("Detail view", 367, 321, 11, "#94AAC7"); return;
        }
        PutWidget(90, 58, 540, 275);
        if (key is "notes" or "calculator") Pointer(270, 133, p < .25);
        if (key == "tasks") Pointer(238, p < .5 ? 136 : 169, p is > .25 and < .75);
        if (key is "focus" or "stopwatch") Pointer(p < .8 ? 250 : 415, 238, p < .2 || p > .8);
        if (key == "sound-desk") { KeyBadge("Volume", 36, 81); Meter(40, 114, 96, data.Volume); }
    }
    string OriginalDashboard()
    {
        string text = mod.Name.ToLowerInvariant();
        return text.Contains("sound") ? "sound-desk" : text.Contains("resource") ? "resource-desk" : text.Contains("day planner") ? "day-planner" : text.Contains("window desk") ? "window-desk" : "custom";
    }
    void PutWidget(double x, double y, double width, double height)
    {
        widget!.Tick();
        var box = new Viewbox { Width = width, Height = height, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = widget.View };
        widgetHost = box;
        At(box, x, y); canvas.Children.Add(box);
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
        Box(0, 0, 720, 340, background, 0); Box(15, 18, 690, 32, panel, 5);
        Text("Apps   Files   Mods", 30, 25, 13, text); Text("Thu, Oct 29     08:25", 536, 25, 12, text);
        Box(39, 73, 307, 231, panel, 11); Text("Apps", 58, 88, 22, text);
        for (int i = 0; i < 6; i++) { double x = 63 + i % 3 * 85, y = 136 + i / 3 * 77; var icon = UiIcons.Icon(new[] { "apps", "folder", "mods", "window", "profiles", "controls" }[i], 28); icon.SetValue(Control.ForegroundProperty, Theme.Brush(color)); At(icon, x + 10, y); Target.Children.Add(icon); Text(new[] { "Apps", "Files", "Mods", "Windows", "Profiles", "Settings" }[i], x, y + 39, 12, text); }
        Box(398, 83, 275, 170, panel, 11); Text("08:25", 421, 103, 47, text); Text("Thursday, October 29", 425, 167, 15, text); Box(425, 204, 205, 7, color, 3);
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
    void Text(string text, double x, double y, double size, string color = "#EAF2FF") { var label = new TextBlock { Text = text, FontSize = size, Foreground = Theme.Brush(color), FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.NoWrap }; At(label, x, y); Target.Children.Add(label); }
    void Icon(string kind, double x, double y, double size) { var icon = UiIcons.Icon(kind, size); At(icon, x, y); Target.Children.Add(icon); }
    static void At(UIElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); }
    static double Smooth(double p) => p * p * (3 - 2 * p);
    static Rect Lerp(Rect a, Rect b, double p) => new(a.X + (b.X - a.X) * p, a.Y + (b.Y - a.Y) * p, a.Width + (b.Width - a.Width) * p, a.Height + (b.Height - a.Height) * p);
}
