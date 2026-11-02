using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace Harbor;

// A small, real extruded mesh projected into WPF drawing commands. No D3D scene.
internal sealed class HarborEmblem : FrameworkElement
{
    readonly record struct Face(Point3D[] Points, double Exposure);
    internal readonly record struct Projected(Point[] Points, double Depth, int Shade, bool Metallic);
    static readonly Face[] mesh = BuildMesh();
    static readonly ConcurrentDictionary<Color, (Brush[] Base, Brush[] Sheen)> materials = new();
    static (Brush[] Base, Brush[] Sheen) Materials(Color color)
    {
    var blue = Enumerable.Range(0, 80).Select(i => {
        double t = i / 79d; var a = Theme.Mix(Colors.Black, color, .15); var b = Theme.Mix(Colors.Black, color, .8); var c = Theme.Mix(color, Colors.White, .65);
        var lo = t < .65 ? a : b; var hi = t < .65 ? b : c; double mix = t < .65 ? t / .65 : (t - .65) / .35;
        var brush = new SolidColorBrush(Color.FromRgb((byte)(lo.R + (hi.R - lo.R) * mix), (byte)(lo.G + (hi.G - lo.G) * mix), (byte)(lo.B + (hi.B - lo.B) * mix))); brush.Freeze(); return (Brush)brush;
    }).ToArray();
    var sheen = blue.Cast<SolidColorBrush>().Select(b => {
        var c = b.Color;
        var brush = new LinearGradientBrush { StartPoint = new Point(.15, 0), EndPoint = new Point(.85, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb((byte)Math.Min(255, c.R * 1.06 + 3), (byte)Math.Min(255, c.G * 1.06 + 3), (byte)Math.Min(255, c.B * 1.06 + 3)), 0));
        brush.GradientStops.Add(new GradientStop(c, .38)); brush.GradientStops.Add(new GradientStop(Color.FromRgb((byte)(c.R * .82), (byte)(c.G * .84), (byte)(c.B * .88)), 1)); brush.Freeze(); return (Brush)brush;
    }).ToArray(); return (blue, sheen);
    }
    bool spinning;
    int spinFPS;
    AnimationTiming? timing;
    readonly FrameTiming frames = new();
    internal object FramePacing => frames.Report();
    internal bool SampleFrames { get; set; }
    internal Color? Tint { get; set; }
    long firstFrame, lastFrame;
    internal int RenderedFrames { get; private set; }
    internal double RenderedFPS => lastFrame > firstFrame ? (RenderedFrames - 1) * (double)System.Diagnostics.Stopwatch.Frequency / (lastFrame - firstFrame) : 0;
    internal static readonly DependencyProperty TurnProperty = DependencyProperty.Register(nameof(Turn), typeof(double), typeof(HarborEmblem), new FrameworkPropertyMetadata(-16d, FrameworkPropertyMetadataOptions.AffectsRender));
    internal double Turn { get => (double)GetValue(TurnProperty); set => SetValue(TurnProperty, value); }
    internal bool IsSpinning => spinning;
    internal HarborEmblem(double size = 190) { Width = Height = size; IsHitTestVisible = false; Theme.RegisterEmblem(this); Unloaded += (_, _) => Spin(false); }
    internal void Spin(bool enabled, int fps = 120)
    {
        fps = Math.Clamp(fps, 80, 144);
        if (enabled == spinning && (!enabled || fps == spinFPS)) return; spinning = enabled; spinFPS = fps;
        if (enabled && SampleFrames) { firstFrame = lastFrame = 0; RenderedFrames = 0; frames.Reset(); }
        timing?.Dispose(); timing = enabled ? new AnimationTiming() : null;
        double current = Turn; BeginAnimation(TurnProperty, null); Turn = current % 360;
        if (!enabled) return;
        var animation = new DoubleAnimation(Turn, Turn + 360, TimeSpan.FromSeconds(20)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(animation, Math.Clamp(fps, 80, 144)); BeginAnimation(TurnProperty, animation);
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (SampleFrames) { frames.Frame(); long now = System.Diagnostics.Stopwatch.GetTimestamp(); if (RenderedFrames++ == 0) firstFrame = now; lastFrame = now; }
        double width = ActualWidth > 0 ? ActualWidth : Width, height = ActualHeight > 0 ? ActualHeight : Height;
        if (!double.IsFinite(width) || width <= 0 || height <= 0) return;
        if (Theme.Dragon) { DragonArt.Draw(dc, width, height, Turn, tint: Tint); return; }
        var paint = materials.GetOrAdd(Tint ?? Theme.CurrentColors.Logo, Materials);
        foreach (var face in Project(Turn, width, height))
        {
            var geometry = new StreamGeometry(); using (var path = geometry.Open()) { path.BeginFigure(face.Points[0], true, true); path.PolyLineTo(face.Points.Skip(1).ToArray(), true, false); } geometry.Freeze();
            dc.DrawGeometry(face.Metallic ? paint.Sheen[face.Shade] : paint.Base[face.Shade], null, geometry);
        }
    }
    internal static Projected[] Project(double turn, double width, double height)
    {
        double yaw = turn * Math.PI / 180, pitch = -12 * Math.PI / 180, roll = -5 * Math.PI / 180, scale = Math.Min(width, height) / 224;
        double cy = Math.Cos(yaw), sy = Math.Sin(yaw), cp = Math.Cos(pitch), sp = Math.Sin(pitch), cr = Math.Cos(roll), sr = Math.Sin(roll);
        var light = new Vector3D(-.5, -.75, 1); light.Normalize();
        var half = light + new Vector3D(0, 0, 1); half.Normalize();
        var result = new List<Projected>(mesh.Length);
        foreach (var face in mesh)
        {
            var p = face.Points.Select(Rotate).ToArray(); var normal = Vector3D.CrossProduct(p[1] - p[0], p[2] - p[0]);
            if (normal.LengthSquared < .001) continue; normal.Normalize(); if (normal.Z <= .015) continue;
            double illumination = Math.Clamp(Vector3D.DotProduct(normal, light), 0, 1);
            double reflection = Math.Pow(Math.Max(0, Vector3D.DotProduct(normal, half)), 60);
            double rim = Math.Pow(1 - normal.Z, 2) * .035;
            int shade = (int)Math.Clamp((.18 + illumination * .49 + reflection * .26 + rim) * face.Exposure * 79, 0, 79);
            result.Add(new Projected(p.Select(v => new Point(width / 2 + v.X * scale * 420 / (420 - v.Z), height / 2 + v.Y * scale * 420 / (420 - v.Z))).ToArray(), p.Average(v => v.Z), shade, face.Exposure >= 1));
        }
        return result.OrderBy(p => p.Depth).ToArray();
        Point3D Rotate(Point3D p) { double x = p.X * cy + p.Z * sy, z = -p.X * sy + p.Z * cy, y = p.Y * cp - z * sp; z = p.Y * sp + z * cp; return new Point3D(x * cr - y * sr, x * sr + y * cr, z); }
    }
    static Face[] BuildMesh()
    {
        var faces = new List<Face>();
        // Three separated, folded gate pieces around an open center. Each piece
        // has two sloped faces and a solid back; the reference's monogram is gone.
        for (int piece = 0; piece < 3; piece++)
        {
            double angle = piece * Math.PI * 2 / 3;
            var shape = new[] { new Point(-58, -37), new Point(0, -74), new Point(58, -37), new Point(32, -22), new Point(0, -42), new Point(-32, -22) };
            Point3D P(int index, bool front) { var p = shape[index]; return new Point3D(p.X * Math.Cos(angle) - p.Y * Math.Sin(angle), p.X * Math.Sin(angle) + p.Y * Math.Cos(angle), front ? index >= 3 ? 22 : 10 : -8); }
            Beveled(P(0, true), P(1, true), P(4, true), P(5, true));
            Beveled(P(1, true), P(2, true), P(3, true), P(4, true));
            Quad(P(5, false), P(4, false), P(1, false), P(0, false), .76);
            Quad(P(4, false), P(3, false), P(2, false), P(1, false), .76);
            for (int edge = 0; edge < 6; edge++) { int next = (edge + 1) % 6; Quad(P(next, true), P(edge, true), P(edge, false), P(next, false), edge >= 3 ? .60 : .78); }
        }
        return faces.ToArray();
        void Quad(Point3D a, Point3D b, Point3D c, Point3D d, double exposure = 1) => faces.Add(new Face(new[] { a, b, c, d }, exposure));
        void Beveled(Point3D a, Point3D b, Point3D c, Point3D d)
        {
            var outer = new[] { a, b, c, d }; var center = new Point3D(outer.Average(p => p.X), outer.Average(p => p.Y), outer.Average(p => p.Z));
            var normal = Vector3D.CrossProduct(b - a, c - a); normal.Normalize();
            var inner = outer.Select(p => center + (p - center) * .975 + normal * 1.2).ToArray();
            Quad(inner[0], inner[1], inner[2], inner[3]);
            for (int edge = 0; edge < 4; edge++) { int next = (edge + 1) % 4; Quad(outer[edge], outer[next], inner[next], inner[edge], 1.16); }
        }
    }
}
