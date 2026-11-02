using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static partial class Theme
{
    internal static string Skin { get; private set; } = "harbor";
    internal static bool Dragon => Skin == "tokyo-dragon";
    static readonly List<WeakReference<HarborEmblem>> emblems = new();
    internal static void RegisterEmblem(HarborEmblem emblem) { lock (emblems) { emblems.RemoveAll(w => !w.TryGetTarget(out _)); emblems.Add(new(emblem)); } }
    internal static void SetSkin(string skin)
    {
        string next = skin == "tokyo-dragon" ? skin : "harbor"; if (Skin == next) return; Skin = next; PublishIcon();
        lock (emblems) foreach (var weak in emblems) if (weak.TryGetTarget(out var emblem)) emblem.Dispatcher.BeginInvoke(new Action(emblem.InvalidateVisual));
    }
    internal static void RefreshEmblems()
    {
        PublishIcon(); lock (emblems) foreach (var weak in emblems) if (weak.TryGetTarget(out var emblem)) emblem.Dispatcher.BeginInvoke(new Action(emblem.InvalidateVisual));
    }
    internal static void PublishIcon()
    {
        if (Application.Current == null) return;
        var mark = new HarborEmblem(40); mark.Measure(new Size(40, 40)); mark.Arrange(new Rect(0, 0, 40, 40));
        var bitmap = new RenderTargetBitmap(40, 40, 96, 96, PixelFormats.Pbgra32); bitmap.Render(mark); bitmap.Freeze(); Application.Current.Resources["Harbor.Icon"] = bitmap;
    }
}
