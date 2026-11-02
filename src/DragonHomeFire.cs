using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Harbor;

// The same closed contour is flame, window surface and content clip.
// Its points flow from the mouth into Home's rounded perimeter.
internal sealed class DragonHomeFire : FrameworkElement
{
    const int Steps = 48;
    internal Point Origin { get; set; }
    internal double LogoSize { get; set; } = 190;
    internal Color? LogoTint { get; set; }
    internal Color? FireTint { get; set; }
    internal Color? PanelTint { get; set; }
    internal Color? BorderTint { get; set; }
    internal Rect HomeBounds { get; set; }
    internal Point RenderOffset { get; set; }
    internal double CornerRadius { get; set; } = 22;
    Point[]? perimeter;
    Rect cachedBounds;
    double cachedRadius;
    Geometry? cachedSurface;
    double cachedProgress = -1;
    internal static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(DragonHomeFire), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    internal double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    internal static double Ease(double value) { double p = Math.Clamp(value, 0, 1); return p * p * (3 - 2 * p); }
    internal static double ActorOpacity(double p) => Ease(p / .16) * (1 - Ease((p - .64) / .24));
    internal static double FireOpacity(double p) => Ease((p - .25) / .12) * (1 - Ease((p - .68) / .30));
    internal static double Tilt(double p) => Roll(p);
    internal static double Roll(double p) => -5 - 12 * Ease((p - .09) / .18);
    internal static double Morph(double p) => Ease((p - .49) / .47);
    internal static double ContentReveal(double p) => Ease((p - .96) / .04);
    internal static double IdleOpacity(double p) => 1 - Ease(p / .16);
    internal DragonHomeFire() { IsHitTestVisible = false; }
    Rect Bounds => HomeBounds.Width > 0 ? HomeBounds : new Rect(45, 35, Math.Max(10, Width - 90), Math.Max(10, Height - 70));
    internal Point Mouth
    {
        get { double size = LogoSize * 1.12; var mouth = DragonArt.Mouth(size, size, 0, 0, Roll(Progress) - DragonArt.ProfileHeading); mouth.Offset(Origin.X - size / 2, Origin.Y - size / 2); return mouth; }
    }
    internal Vector Direction { get { double angle = Roll(Progress) * Math.PI / 180; return new Vector(Math.Cos(angle), Math.Sin(angle)); } }
    internal Geometry Surface
    {
        get
        {
            var bounds = Bounds; double p = Progress;
            if (cachedSurface != null && cachedProgress == p && cachedBounds == bounds && cachedRadius == CornerRadius) return cachedSurface;
            if (perimeter == null || cachedBounds != bounds || cachedRadius != CornerRadius) perimeter = Perimeter(bounds, CornerRadius);
            cachedBounds = bounds; cachedRadius = CornerRadius; cachedProgress = p;
            double morph = Morph(p); var mouth = Mouth; var direction = Direction; var normal = new Vector(-direction.Y, direction.X);
            double reach = Math.Min(Math.Max(bounds.Width * .46, 220), Math.Max(230, bounds.Height * .78)) * Ease((p - .25) / .30);
            double width = Math.Min(bounds.Height * .15, 108) * Ease((p - .25) / .30);
            var points = new Point[Steps];
            for (int i = 0; i < Steps; i++)
            {
                double phase = i / (double)Steps, t = phase <= .5 ? phase * 2 : (1 - phase) * 2, side = phase <= .5 ? -1 : 1;
                double profile = Math.Pow(Math.Max(0, Math.Sin(t * Math.PI)), 1.12) * (.27 + .73 * Math.Sqrt(t));
                double flicker = Math.Pow(.5 + .5 * Math.Sin(t * 29 - p * 38 + side * 2), 4);
                double tongues = .72 + .78 * flicker + .12 * Math.Sin(t * 53 - p * 51);
                var flame = mouth + direction * (reach * (t + .035 * flicker * Math.Sin(t * Math.PI))) + normal * (width * profile * side * tongues + Math.Sin(t * 11 - p * 26) * width * .12 * t);
                var target = perimeter[i]; points[i] = new Point(flame.X + (target.X - flame.X) * morph, flame.Y + (target.Y - flame.Y) * morph);
            }
            cachedSurface = morph >= .99999 ? new RectangleGeometry(bounds, CornerRadius, CornerRadius) : Curve(points);
            cachedSurface.Freeze(); return cachedSurface;
        }
    }
    internal Geometry ContentClip(Point panelTopLeft)
    {
        var result = new GeometryGroup { Transform = new TranslateTransform(-panelTopLeft.X, -panelTopLeft.Y) }; result.Children.Add(Surface); result.Freeze(); return result;
    }
    protected override void OnRender(DrawingContext dc)
        => DrawFrame(dc);
    internal void DrawFrame(DrawingContext dc)
    {
        double p = Progress; if (p <= 0) return;
        dc.PushTransform(new TranslateTransform(-RenderOffset.X, -RenderOffset.Y));
        var shape = Surface; var mouth = Mouth; var direction = Direction; double morph = Morph(p), heat = FireOpacity(p), cool = Ease((p - .66) / .32);
        var ink = FireTint ?? Theme.CurrentColors.Fire;
        var panel = PanelTint ?? Theme.Panel.Color;
        var core = Theme.Mix(ink, Color.FromRgb(255, 244, 174), .78);
        var outer = Theme.Mix(Colors.Black, ink, .63);
        var body = new LinearGradientBrush { StartPoint = new Point(.10, .80), EndPoint = new Point(.90, .12) };
        body.GradientStops.Add(new GradientStop(Theme.Mix(core, panel, cool), 0));
        body.GradientStops.Add(new GradientStop(Theme.Mix(ink, panel, cool), .38));
        body.GradientStops.Add(new GradientStop(Theme.Mix(outer, panel, cool), .82));
        body.GradientStops.Add(new GradientStop(Theme.Mix(outer, panel, cool), 1)); body.Freeze();
        dc.PushOpacity(Ease((p - .25) / .10)); dc.DrawGeometry(body, null, shape);
        if (heat > .001)
        {
            dc.PushClip(shape);
            var normal = new Vector(-direction.Y, direction.X);
            for (int lick = 0; lick < 3; lick++)
            {
                double length = Math.Min(Bounds.Width * .52, 520) * Ease((p - .25) / .28) * (.70 + .09 * Math.Sin(p * 31 + lick * 1.8));
                var points = new Point[48];
                for (int i = 0; i < points.Length; i++) { double phase = i / (double)points.Length, t = phase <= .5 ? phase * 2 : (1 - phase) * 2, side = phase <= .5 ? -1 : 1; double wave = Math.Sin(t * 15 - p * 42 + lick * 1.9); points[i] = mouth + direction * (length * t) + normal * (Math.Sin(t * 9 - p * 26 + lick * 2) * 25 * t + side * Math.Pow(Math.Sin(t * Math.PI), 1.4) * (10 + lick * 4) * (.6 + .4 * wave)); }
                var transparent = ink; transparent.A = 0; var brush = new LinearGradientBrush { StartPoint = new Point(.08, .8), EndPoint = new Point(.9, .1), GradientStops = { new GradientStop(core, 0), new GradientStop(Theme.Mix(ink, core, .45), .42), new GradientStop(transparent, 1) }, Opacity = heat * (1 - morph) * .65 }; brush.Freeze(); dc.DrawGeometry(brush, null, Curve(points));
            }
            for (int i = 0; i < 3; i++)
            {
                var glow = new RadialGradientBrush(core, Colors.Transparent) { RadiusX = .5, RadiusY = .5 }; glow.Opacity = (.14 + i * .025) * heat; glow.Freeze();
                var point = mouth + direction * (35 + i * 39 + Math.Sin(p * 22 + i) * 15);
                dc.DrawEllipse(glow, null, point, 24 + i * 14, 18 + i * 9);
            }
            for (int i = 0; i < 8; i++)
            {
                double t = (p * 2.6 + i * .139) % 1; var point = mouth + direction * (t * Math.Min(Bounds.Width * .55, 540)) + normal * (Math.Sin(t * 17 + i * 1.9) * 30 * t);
                var ember = new SolidColorBrush(core) { Opacity = heat * (1 - t) * .7 }; ember.Freeze(); dc.DrawEllipse(ember, null, point, 1 + (1 - t) * 2, 1 + (1 - t));
            }
            dc.Pop();
        }
        if (morph > .65) { var edge = new SolidColorBrush(BorderTint ?? Theme.Line.Color) { Opacity = Ease((morph - .65) / .35) }; edge.Freeze(); dc.DrawGeometry(null, new Pen(edge, 1), shape); }
        dc.Pop();
        double actor = ActorOpacity(p);
        if (actor > .001)
        {
            double size = LogoSize * 1.12; dc.PushOpacity(actor); dc.PushTransform(new TranslateTransform(Origin.X - size / 2, Origin.Y - size / 2));
            DragonArt.Draw(dc, size, size, 0, 0, LogoTint, Roll(p) - DragonArt.ProfileHeading); dc.Pop(); dc.Pop();
        }
        dc.Pop();
    }
    static Geometry Curve(Point[] points)
    {
        var geometry = new StreamGeometry(); using (var path = geometry.Open())
        {
            path.BeginFigure(points[0], true, true);
            for (int i = 0; i < points.Length; i++) { var previous = points[(i + points.Length - 1) % points.Length]; var current = points[i]; var next = points[(i + 1) % points.Length]; var after = points[(i + 2) % points.Length]; path.BezierTo(current + (next - previous) * .12, next - (after - current) * .12, next, true, false); }
        }
        return geometry;
    }
    static Point[] Perimeter(Rect bounds, double radius)
    {
        double rx = bounds.Width / 2, ry = bounds.Height / 2, r = Math.Min(radius, Math.Min(rx, ry)); var center = new Point(bounds.X + rx, bounds.Y + ry);
        return Enumerable.Range(0, Steps).Select(i => {
            double angle = Math.PI + i * Math.PI * 2 / Steps, dx = Math.Cos(angle), dy = Math.Sin(angle), low = 0, high = Math.Sqrt(rx * rx + ry * ry) + 1;
            for (int n = 0; n < 18; n++) { double mid = (low + high) / 2, qx = Math.Abs(dx * mid) - rx + r, qy = Math.Abs(dy * mid) - ry + r; double distance = Math.Min(Math.Max(qx, qy), 0) + Math.Sqrt(Math.Pow(Math.Max(qx, 0), 2) + Math.Pow(Math.Max(qy, 0), 2)) - r; if (distance > 0) high = mid; else low = mid; }
            return center + new Vector(dx * low, dy * low);
        }).ToArray();
    }
}
