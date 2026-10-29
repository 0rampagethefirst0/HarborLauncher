using System;
using System.Collections.Generic;

namespace Harbor;

// Deterministic, in-memory examples for catalog recordings. No device or user data.
internal sealed class WidgetDemoData
{
    internal double Phase;
    internal DateTimeOffset Now => new DateTimeOffset(2026, 10, 29, 12, 24, 59, TimeSpan.Zero).AddSeconds(Phase * 5);
    internal double Cpu => 24 + 17 * Math.Sin(Phase * Math.PI * 3);
    internal uint Ram => (uint)(42 + Phase * 3);
    internal double Volume => Phase < .45 ? 36 + Phase * 80 : Phase < .65 ? 0 : 68;
    internal double Elapsed => Phase < .15 ? 0 : Phase < .8 ? (Phase - .15) * 5 : 0;
    const string SampleNotes = "Finish the icon set\nReview the new desktop\nTry the focus timer";
    internal string Notes => SampleNotes[..Math.Min(SampleNotes.Length, (int)(Phase * 100))];
    internal IReadOnlyList<string> Clipboard => new[] { "#83DFD0", "https://example.com/ideas", "Try the new desktop layout" }[..Math.Clamp((int)(Phase * 4) + 1, 1, 3)];
    internal double Number(string source) => source.ToLowerInvariant() switch {
        "cpu" => Cpu, "ram" => Ram, "volume" => Volume, "windows" => 3, "notifications" => 2,
        "disk.c.free" => 642, "disk.c.total" => 1000, "disk.c.used" => 35.8, _ => 0
    };
}
