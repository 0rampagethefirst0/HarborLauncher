using System;
using System.Windows;

namespace Harbor;

internal sealed partial class ModDemoScene
{
    void FeatureScene()
    {
        double p = data.Phase, on = Smooth(Math.Clamp((p - .12) / .2, 0, 1)); string component = mod.Component;
        if (component == "desktop-icons") {
            double progress = Smooth(Math.Clamp((p - .20) / .48, 0, 1)); homeDemo!.PreparePreview(progress);
            homeHost = new System.Windows.Controls.Viewbox { Width = 700, Height = 332, Child = homeView, Stretch = System.Windows.Media.Stretch.Uniform }; homeView!.Width = 1280; homeView.Height = 900; At(homeHost, 10, 3); canvas.Children.Add(homeHost);
            Instruction = "Click the emblem. Your desktop opens by category."; return;
        }
        AppWindow(new Rect(28, 64, 655, 260), "Your workspace", 1, true);
        Instruction = original.Description;
        if (component is "top-dock" or "dock-shortcuts" or "dock-clock" or "dock-stats" or "active-title" or "network-control" or "volume-control" or "media-controls" or "dock-dividers")
        {
            if (on < .05) { Pointer(603, 33, true); return; } Bar();
            if (component == "media-controls") { Box(264, 20, 176, 30, "#1B2739", 7); Icon("previous", 282, 26, 18); Icon(p > .5 ? "stop" : "play", 338, 26, 18); Icon("next", 394, 26, 18); Text("Control your music player", 247, 94, 19); Pointer(343, 31, p > .3); }
            else if (component == "dock-clock") { Box(230, 20, 285, 30, "#1B2739", 7); Text("Thu, Oct 29", 244, 26, 13); Text(p < .5 ? "08:25" : "08:25:42", 400, 26, 13); }
            else if (component == "dock-stats") { Text("CPU " + data.Cpu.ToString("0") + "%   RAM " + data.Ram.ToString("0") + "%", 416, 26, 13); }
            else if (component == "active-title") { Box(164, 20, 320, 30, "#1B2739", 6); Text(p < .5 ? "Browser · Project research" : "Harbor Files · Projects", 180, 26, 13); }
            else if (component is "network-control" or "volume-control") { Box(426, 58, 260, 218, "#1D283A", 12); Text(component == "volume-control" ? "Audio" : "Network", 448, 78, 23); Icon(component == "volume-control" ? "volume" : "network", 578, 26, 17); Text(component == "volume-control" ? "System volume" : "Connected · Wi-Fi", 448, 126, 14); Meter(448, 166, 202, component == "volume-control" ? 25 + p * 55 : 85); }
            else if (component == "dock-dividers") { double gap = 22 + p * 64; Icon("previous", 278 - gap, 27, 16); Icon("play", 302 - gap, 27, 16); Box(337, 23, 1 + p * 4, 24, "#9BBEFF", 1); Icon("next", 360 + gap, 27, 16); Pointer(339, 34, p > .3); }
            else if (component == "dock-shortcuts") { foreach (var item in new[] { ("folder", 410d), ("mods", 460d), ("notifications", 510d), ("controls", 560d) }) Icon(item.Item1, item.Item2, 26, 18); }
            else { Text("Drag pieces to your own layout", 177, 112, 20); var moving = Box(270 + p * 190, 21, 80, 28, "#395A77", 6); Text("Play / pause", 281 + p * 190, 28, 11); Pointer(307 + p * 190, 34, p > .4); }
            return;
        }
        if (component is "taskbar" or "taskbar-folders" or "taskbar-dividers")
        {
            if (on < .05) return; Dock();
            if (component == "taskbar-folders") { Box(327, 281, 58, 52, "#33425B", 9); Icon("folder", 341, 287, 27); Text("Games", 337, 317, 11); if (p > .3) { Box(254, 95, 294, 168, "#1D283A", 12); Text("Games", 273, 111, 20); double size = 26 + p * 18; AppIcon("Game", 279, 158, size); AppIcon("Notes", 365, 158, size); AppIcon("Browser", 451, 158, size); Text("Change app size in Bulky", 273, 233, 12); } }
            if (component == "taskbar-dividers") { double width = 18 + p * 66; Box(351, 287, 1 + p * 4, 37, "#9BBEFF", 1); KeyBadge("Spacing " + width.ToString("0") + " px", 271, 189); Pointer(354, 305, true); }
            return;
        }
        if (component is "file-manager" or "file-preview" or "file-thumbnails" or "collection-tabs")
        {
            if (on < .05) return; AppWindow(new Rect(35, 57, 650, 273), "Harbor Files · Projects", 3, true);
            Text("Projects", 56, 112, 22); Text("Home     Projects     Games", 56, 87, 13);
            if (component == "collection-tabs") { Box(249, 82, 93, 25, "#3D587B", 5); Text("Games", 269, 86, 13); AppIcon("Game", 86, 169, 48); AppIcon("Notes", 202, 169, 48); AppIcon("Browser", 318, 169, 48); Text("The same apps as your taskbar folder", 63, 272, 15); }
            else if (component == "file-thumbnails") { for (int i = 0; i < 3; i++) { Box(61 + i * 185, 158, 154, 101, i == 1 ? "#597756" : "#4F779C", 8); Icon("image", 116 + i * 185, 184, 42); Text(new[] { "Lake.png", "Forest.jpg", "Sky.png" }[i], 84 + i * 185, 274, 14); } }
            else { FileRow(60, 159, "Designs", true); FileRow(60, 201, "Notes.txt", false); FileRow(60, 243, "Photo.png", false); if (component == "file-preview") { Box(413, 109, 246, 199, "#25334B", 7); Text("Photo.png", 431, 125, 16); Box(432, 157, 207, 111, "#4F779C", 7); Icon("image", 503, 187, 53); } }
            return;
        }
        if (component == "multi-monitor") { Box(34, 78, 304, 203, "#172236", 10); Box(369, 78, 304, 203, "#172236", 10); Text("Main monitor", 105, 98, 18); Text("Second monitor", 433, 98, 18); Box(49, 246, 274, 25, "#344D6A", 6); if (on > .1) Box(384, 246, 274, 25, "#344D6A", 6); Icon("apps", 167, 250, 17); if (on > .1) Icon("apps", 502, 250, 17); return; }
        if (component == "notification-center") { if (on < .05) return; Box(386, 65, 302, 265, "#1D283A", 12); Text("Notifications", 405, 85, 23); if (p < .75) { Box(404, 132, 264, 67, "#2C3E58", 7); Text("Focus session finished", 416, 148, 15); Text("Time for a break", 416, 172, 12); Box(404, 210, 264, 60, "#2C3E58", 7); Text("Build complete", 416, 230, 15); } else Text("You're all caught up", 419, 164, 18); Text("Clear all", 599, 295, 12); Pointer(622, 302, p > .7); return; }
        if (component == "quick-settings") { if (on < .05) return; Box(353, 66, 330, 260, "#1D283A", 12); Text("Quick settings", 375, 85, 23); Text("System volume", 375, 130, 14); Meter(375, 159, 274, 68); Text("Music", 375, 236, 14); Box(455, 242, 185 * p, 9, "#83DFD0", 3); Text("Game", 375, 281, 14); Box(455, 287, 148, 9, "#9BBEFF", 3); return; }
    }
}
