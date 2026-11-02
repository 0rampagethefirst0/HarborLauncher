using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Harbor;

internal static class DragonArt
{
    static readonly Geometry outline = Shape("M42,184 C15,148 25,105 55,82 L39,85 L58,65 L50,53 L72,64 C46,42 43,21 42,10 C67,34 76,45 84,59 L88,27 L111,63 C133,72 139,91 157,98 C166,101 175,92 182,100 C191,111 179,123 159,118 C140,116 129,105 112,105 C123,122 127,142 139,145 C148,147 157,140 162,132 C163,148 151,163 132,161 L118,169 L125,156 C106,151 110,129 90,117 L76,120 L87,107 L62,107 L75,95 C48,101 36,126 42,145 C47,161 55,169 61,175");
    static readonly Geometry detail = Shape("M105,81 Q122,80 133,94 L118,90 Z M172,104 Q178,101 178,108 M157,119 L153,130 L148,118 M143,117 L139,125 L134,113 M137,146 L145,137 L147,147 M150,145 L156,137 M131,142 C151,135 171,150 188,145 L177,153 L188,160 C164,157 152,145 132,147 M165,96 C185,73 201,99 192,113 M165,118 C179,132 189,131 199,125 M65,83 L78,84 L70,74");
    static Geometry Shape(string data) { var shape = Geometry.Parse(data); shape.Freeze(); return shape; }
    internal static double ProfileHeading { get { var a = Project(new Point(105, 81), 4, 224, 224, 0, rollDegrees: 0); var b = Project(new Point(172, 105), 4, 224, 224, 0, rollDegrees: 0); return Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI; } }
    static readonly Point[][] paths = new[] { outline, detail }.SelectMany(g => g.GetFlattenedPathGeometry(.8, ToleranceType.Absolute).Figures.Select(f => new[] { f.StartPoint }.Concat(f.Segments.OfType<PolyLineSegment>().SelectMany(s => s.Points)).Concat(f.IsClosed ? new[] { f.StartPoint } : Array.Empty<Point>()).ToArray())).ToArray();
    internal static Point Project(Point point, double z, double width, double height, double turn, double tilt = 0, double? rollDegrees = null)
    {
        double yaw = turn * Math.PI / 180, pitch = (-9 + tilt) * Math.PI / 180, roll = (rollDegrees ?? -5 + tilt * .25) * Math.PI / 180;
        double px = point.X - 112, py = point.Y - 110;
        double x = px * Math.Cos(yaw) + z * Math.Sin(yaw), depth = -px * Math.Sin(yaw) + z * Math.Cos(yaw);
        double y = py * Math.Cos(pitch) - depth * Math.Sin(pitch); depth = py * Math.Sin(pitch) + depth * Math.Cos(pitch);
        double side = Math.Min(width, height), scale = side / 224d * 460 / (460 - depth);
        return new Point(width / 2 + (x * Math.Cos(roll) - y * Math.Sin(roll)) * scale, height / 2 + (x * Math.Sin(roll) + y * Math.Cos(roll)) * scale);
    }
    internal static Point Mouth(double width, double height, double turn = -14, double tilt = 0, double? roll = null) => Project(new Point(151, 132), 4, width, height, turn, tilt, roll);
    internal static void Draw(DrawingContext dc, double width, double height, double turn = -16, double tilt = 0, Color? tint = null, double? roll = null)
    {
        double side = Math.Min(width, height), thickness = Math.Max(1.15, side / 224d * 3.2), frontZ = Math.Cos(turn * Math.PI / 180) >= 0 ? 4 : -4;
        var ink = tint ?? Theme.CurrentColors.Logo;
        Pen Pen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
        var backPen = Pen(Theme.Mix(Colors.Black, ink, .35)); var frontPen = Pen(Theme.Mix(ink, Colors.White, .12));
        var sides = new StreamGeometry(); using (var shape = sides.Open()) foreach (var path in paths) for (int i = 1; i < path.Length; i++) { var a = Project(path[i - 1], -frontZ, width, height, turn, tilt, roll); var b = Project(path[i], -frontZ, width, height, turn, tilt, roll); var c = Project(path[i], frontZ, width, height, turn, tilt, roll); var d = Project(path[i - 1], frontZ, width, height, turn, tilt, roll); shape.BeginFigure(a, true, true); shape.PolyLineTo(new[] { b, c, d }, true, false); } sides.Freeze();
        dc.DrawGeometry(null, backPen, Layer(-frontZ)); var edge = new SolidColorBrush(Theme.Mix(Colors.Black, ink, .52)); edge.Freeze(); dc.DrawGeometry(edge, null, sides); dc.DrawGeometry(null, frontPen, Layer(frontZ));
        StreamGeometry Layer(double z) { var layer = new StreamGeometry(); using (var shape = layer.Open()) foreach (var path in paths) { if (path.Length < 2) continue; shape.BeginFigure(Project(path[0], z, width, height, turn, tilt, roll), false, false); shape.PolyLineTo(path.Skip(1).Select(p => Project(p, z, width, height, turn, tilt, roll)).ToArray(), true, false); } layer.Freeze(); return layer; }
    }
}
