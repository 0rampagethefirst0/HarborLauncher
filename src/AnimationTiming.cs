using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Harbor;

// Request finer scheduling only while motion is actually visible. Every request
// is paired, including when a window is covered or its animation is interrupted.
internal sealed class AnimationTiming : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
    static readonly object gate = new();
    static int users;
    static bool active;
    bool disposed;
    internal AnimationTiming() { lock (gate) { if (users++ == 0) active = timeBeginPeriod(1) == 0; } }
    internal static void For(int milliseconds)
    {
        if (Settings.TestDataDir != null) return;
        var lease = new AnimationTiming(); var end = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds + 100) };
        end.Tick += (_, _) => { end.Stop(); lease.Dispose(); }; end.Start();
    }
    public void Dispose() { lock (gate) { if (disposed) return; disposed = true; if (--users == 0 && active) { timeEndPeriod(1); active = false; } } }
}

internal sealed class FrameTiming
{
    readonly List<double> intervals = new();
    long previous;
    internal int Frames { get; private set; }
    internal void Reset() { intervals.Clear(); previous = 0; Frames = 0; }
    internal void Frame()
    {
        long now = Stopwatch.GetTimestamp(); if (previous > 0 && intervals.Count < 4096) intervals.Add((now - previous) * 1000d / Stopwatch.Frequency);
        previous = now; Frames++;
    }
    internal object Report() => new { Frames, AverageFPS = intervals.Count > 0 ? 1000 / intervals.Average() : 0, MedianMilliseconds = Percentile(.5), P95Milliseconds = Percentile(.95), LongestMilliseconds = intervals.DefaultIfEmpty().Max(), FramesOver25ms = intervals.Count(v => v > 25), FramesOver50ms = intervals.Count(v => v > 50) };
    double Percentile(double p) { if (intervals.Count == 0) return 0; var sorted = intervals.OrderBy(v => v).ToArray(); return sorted[(int)Math.Clamp(Math.Ceiling(sorted.Length * p) - 1, 0, sorted.Length - 1)]; }
}
