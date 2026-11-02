using System;
using System.Windows;

namespace Harbor;

// The desktop and Home share one physical-screen pose during the handoff.
internal readonly record struct EmblemState(Rect Bounds, double Turn)
{
    internal EmblemState RelativeTo(Point windowOrigin, double dpi) => new(new Rect((Bounds.Left - windowOrigin.X) / dpi, (Bounds.Top - windowOrigin.Y) / dpi, Bounds.Width / dpi, Bounds.Height / dpi), Turn);
    internal Point Center => new(Bounds.Left + Bounds.Width / 2, Bounds.Top + Bounds.Height / 2);
    internal static double MorphTurn(double source, double reveal) => source + Math.IEEERemainder(-16 - source, 360) * Math.Clamp(reveal, 0, 1);
    internal static Point Travel(Point from, Point to, double reveal)
    {
        double p = Math.Clamp(reveal, 0, 1);
        return new Point(from.X + (to.X - from.X) * p, from.Y + (to.Y - from.Y) * p);
    }
}
