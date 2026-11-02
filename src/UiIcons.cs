using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Harbor;

internal static class UiIcons
{
    internal static FrameworkElement Icon(string kind, double size = 16)
    {
        string data = kind switch {
            "apps" => "M1.5,1.5 H6.5 V6.5 H1.5 Z M9.5,1.5 H14.5 V6.5 H9.5 Z M1.5,9.5 H6.5 V14.5 H1.5 Z M9.5,9.5 H14.5 V14.5 H9.5 Z",
            "controls" => "M1,4 H5 M9,4 H15 M1,12 H9 M13,12 H15 M7,2 A2,2 0 1 1 7,6 A2,2 0 1 1 7,2 M11,10 A2,2 0 1 1 11,14 A2,2 0 1 1 11,10",
            "mods" => "M8,1 L15,5 L8,9 L1,5 Z M1,8 L8,12 L15,8 M1,11 L8,15 L15,11",
            "profiles" => "M1.5,2.5 H14.5 V13.5 H1.5 Z M5,6 A1.5,1.5 0 1 1 5,9 A1.5,1.5 0 1 1 5,6 M3,12 Q5,8.5 7,12 M10,6 H12 M10,9 H12",
            "appearance" => "M11,1 L15,5 L7,13 L3,9 Z M3,9 Q0,10 1,15 Q6,15 7,13",
            "folder" => "M1.5,3.5 H6 L8,5.5 H14.5 V13.5 H1.5 Z",
            "clock" => "M8,1 A7,7 0 1 0 8,15 A7,7 0 1 0 8,1 M8,4 V8 L11,10",
            "timer" => "M5,1 H11 M8,1 V3 M12,4 L14,2 M8,3 A6,6 0 1 0 8,15 A6,6 0 1 0 8,3 M8,6 V9 L10,10",
            "stats" => "M2,14 V8 H5 V14 M6.5,14 V2 H9.5 V14 M11,14 V5 H14 V14 M1,14 H15",
            "drive" => "M3,2 H13 L15,10 V14 H1 V10 Z M1,10 H15 M11,12 H11.1 M13,12 H13.1",
            "tasks" => "M1,4 L3,6 L6,2 M8,4 H15 M1,11 L3,13 L6,9 M8,11 H15",
            "calculator" => "M3,1 H13 V15 H3 Z M5,3 H11 V6 H5 Z M5,9 H5.1 M8,9 H8.1 M11,9 H11.1 M5,12 H5.1 M8,12 H8.1 M11,12 H11.1",
            "grid" => "M1.5,1.5 H6.5 V6.5 H1.5 Z M9.5,1.5 H14.5 V6.5 H9.5 Z M1.5,9.5 H6.5 V14.5 H1.5 Z M9.5,9.5 H14.5 V14.5 H9.5 Z",
            "file" => "M3,1 H9 L13,5 V15 H3 Z M9,1 V5 H13 M5,8 H11 M5,11 H10",
            "image" => "M1.5,2 H14.5 V14 H1.5 Z M2,12 L6,7 L9,10 L11,8 L14,12 M11,4 A1,1 0 1 0 11,6 A1,1 0 1 0 11,4",
            "code" => "M5,4 L1,8 L5,12 M11,4 L15,8 L11,12 M9,2 L7,14",
            "archive" => "M2,3 H14 V15 H2 Z M1,1 H15 V5 H1 Z M6,8 H10 V11 H6 Z",
            "video" => "M1,3 H11 V13 H1 Z M11,6 L15,3 V13 L11,10",
            "audio" => "M6,12 V3 L14,1 V10 M6,10 C0,9 0,16 5,14 M14,8 C8,7 8,14 13,12",
            "volume" => "M1,6 H4 L8,2 V14 L4,10 H1 Z M10,5 Q13,8 10,11 M12,2 Q18,8 12,14",
            "previous" => "M2,2 V14 M14,2 L5,8 L14,14 Z",
            "play" => "M4,2 L14,8 L4,14 Z",
            "next" => "M14,2 V14 M2,2 L11,8 L2,14 Z",
            "network" => "M1,5 Q8,-1 15,5 M3,8 Q8,3 13,8 M5,11 Q8,8 11,11 M8,14 H8.1",
            "notifications" => "M3,11 V7 C3,0 13,0 13,7 V11 L15,13 H1 Z M6,15 H10",
            "grip" => "M5,3 H5.1 M11,3 H11.1 M5,8 H5.1 M11,8 H11.1 M5,13 H5.1 M11,13 H11.1",
            "up" => "M3,10 L8,5 L13,10",
            "down" => "M3,6 L8,11 L13,6",
            "plus" => "M2,8 H14 M8,2 V14",
            "trash" => "M2,4 H14 M5,4 V2 H11 V4 M4,5 V14 H12 V5 M6,7 V12 M10,7 V12",
            "search" => "M7,1 A6,6 0 1 0 7,13 A6,6 0 1 0 7,1 M11,11 L15,15",
            "pin" => "M5,1.5 H11 L10,6 L13,9 H3 L6,6 Z M8,9 V15",
            "unpin" => "M6,1.5 H11 L10,6 L13,9 H8 M5,5 L3,9 H5 M8,11 V15 M1,1 L15,15",
            "left" => "M10,3 L5,8 L10,13 M5,8 H15",
            "right" => "M6,3 L11,8 L6,13 M1,8 H11",
            "open" => "M8,2 H14 V8 M14,2 L6,10 M5,3 H2 V14 H13 V11",
            "close" => "M4,4 L12,12 M12,4 L4,12",
            "stop" => "M5,1.5 H11 L14.5,5 V11 L11,14.5 H5 L1.5,11 V5 Z M5,5 L11,11 M11,5 L5,11",
            "minimize" => "M3,12 H13",
            "maximize" => "M2.5,2.5 H13.5 V13.5 H2.5 Z",
            "restore" => "M5.5,2.5 H13.5 V10.5 M2.5,5.5 H10.5 V13.5 H2.5 Z",
            "game" => "M4,5 H12 Q14,5 15,12 Q15,15 11,11 H5 Q1,15 1,12 Q2,5 4,5 Z M3,8 H7 M5,6 V10 M10,7 H10.1 M12,9 H12.1",
            "power" => "M8,1 V8 M4,3 A6,6 0 1 0 12,3",
            "refresh" => "M13,5 A5.5,5.5 0 1 0 13,11 M13,1 V5 H9",
            "copy" => "M5,1.5 H14.5 V11 H11 M1.5,5 H11 V14.5 H1.5 Z",
            "cut" => "M2,1 L13,14 M14,1 L8,8 M4,10 A2.5,2.5 0 1 1 4,15 A2.5,2.5 0 1 1 4,10",
            "typing" => "M1.5,3 H14.5 V13 H1.5 Z M4,6 H4.2 M7,6 H7.2 M10,6 H10.2 M4,9 H4.2 M7,9 H7.2 M10,9 H12 M5,11 H11",
            "opacity" => "M8,1 A7,7 0 1 0 8,15 A7,7 0 1 0 8,1 M8,1 V15 M8,4 H13 M8,7 H14 M8,10 H14 M8,13 H12",
            "time" => "M8,1 A7,7 0 1 0 8,15 A7,7 0 1 0 8,1 M8,4 V8 L11,10",
            "motion" => "M2,4 H7 M1,8 H6 M2,12 H7 M10,3 L15,8 L10,13",
            "pointer" => "M3,1 L14,9 L9,10 L7,15 Z",
            _ => "M1.5,3 H14.5 V13.5 H1.5 Z M1.5,6 H14.5"
        };
        var path = new Path { Data = Geometry.Parse(data), Stroke = kind is "stop" or "power" ? Theme.Brush("#ED9B9B") : Theme.Accent, StrokeThickness = 1.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        var canvas = new Canvas { Width = 16, Height = 16, IsHitTestVisible = false }; canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
    }
    internal static FrameworkElement Label(string text, double size = 12, string? icon = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal }; var image = Icon(icon ?? Kind(text)); image.Margin = new Thickness(0, 0, 9, 0); image.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(image); row.Children.Add(Theme.Label(text, size)); return row;
    }
    internal static string Kind(string text)
    {
        string s = text.ToLowerInvariant();
        if (s == "apps") return "apps";
        if (s.Contains("controls") || s.Contains("settings")) return "controls";
        if (s.StartsWith("mods")) return "mods";
        if (s.Contains("profiles")) return "profiles";
        if (s.Contains("appearance")) return "appearance";
        if (s.Contains("typing")) return "typing";
        if (s.Contains("click-through")) return "pointer";
        if (s.Contains("opacity")) return "opacity";
        if (s.Contains("after leaving")) return "time";
        if (s.Contains("icon size")) return "apps";
        if (s.Contains("motion")) return "motion";
        if (s.StartsWith("end task")) return "stop";
        if (s.StartsWith("close")) return "close";
        if (s.StartsWith("exit harbor")) return "power";
        if (s.StartsWith("unpin")) return "unpin";
        if (s.StartsWith("pin") || s == "always on top") return "pin";
        if (s == "move left") return "left";
        if (s == "move right") return "right";
        if (s.StartsWith("treat as game")) return "game";
        if (s.StartsWith("refresh")) return "refresh";
        if (s == "copy" || s == "paste") return "copy";
        if (s == "cut") return "cut";
        if (s.Contains("files")) return "folder";
        if (s.StartsWith("open")) return "open";
        if (s == "minimize") return "minimize";
        if (s == "maximize") return "maximize";
        if (s == "restore") return "restore";
        return "window";
    }
}
