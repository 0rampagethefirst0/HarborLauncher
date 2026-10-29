using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Harbor;

internal readonly record struct DockSlot(string Id, double Left, double Width)
{
    internal double Center => Left + Width / 2;
}

internal static class DockPlacement
{
    // Legacy layouts keep their three alignments until a button is dragged.
    // Saved positions are fractions of the usable width, shared across monitors.
    internal static List<DockSlot> Arrange(IReadOnlyList<BarItem> items, IReadOnlyDictionary<string, double> widths, double available, string? moving = null, double? position = null)
    {
        var visible = items.Where(i => widths.ContainsKey(i.Id)).ToList();
        if (visible.Count == 0) return new();
        double width = Math.Max(available, visible.Sum(i => widths[i.Id]));
        var desired = new Dictionary<string, double>();
        foreach (string group in new[] { "left", "center", "right" }) {
            var automatic = visible.Where(i => i.Group == group && i.Position == null && i.Id != moving).ToList();
            double total = automatic.Sum(i => widths[i.Id]);
            double left = group == "left" ? 0 : group == "center" ? (width - total) / 2 : width - total;
            foreach (var item in automatic) { desired[item.Id] = left; left += widths[item.Id]; }
        }
        foreach (var item in visible) {
            double? fraction = item.Id == moving && position.HasValue ? position : item.Position;
            if (fraction.HasValue) desired[item.Id] = Math.Clamp(fraction.Value * width - widths[item.Id] / 2, 0, width - widths[item.Id]);
            else if (!desired.ContainsKey(item.Id)) desired[item.Id] = 0;
        }
        // Sort by centers so dropping on either half of an icon inserts on that side.
        var ordered = visible.OrderBy(i => i.Id == moving && position.HasValue ? Math.Clamp(position.Value, 0, 1) * width : desired[i.Id] + widths[i.Id] / 2).ToList();
        var slots = ordered.Select(i => new DockSlot(i.Id, desired[i.Id], widths[i.Id])).ToArray();
        int anchor = ordered.FindIndex(i => i.Id == moving);
        if (anchor >= 0) {
            double before = slots.Take(anchor).Sum(s => s.Width), after = slots.Skip(anchor + 1).Sum(s => s.Width);
            slots[anchor] = slots[anchor] with { Left = Math.Clamp(slots[anchor].Left, before, Math.Max(before, width - slots[anchor].Width - after)) };
            double edge = slots[anchor].Left;
            for (int i = anchor - 1; i >= 0; i--) { before -= slots[i].Width; double min = Math.Max(0, before); slots[i] = slots[i] with { Left = Math.Clamp(slots[i].Left, min, Math.Max(min, edge - slots[i].Width)) }; edge = slots[i].Left; }
            edge = slots[anchor].Left + slots[anchor].Width;
            for (int i = anchor + 1; i < slots.Length; i++) { after -= slots[i].Width; slots[i] = slots[i] with { Left = Math.Clamp(slots[i].Left, edge, Math.Max(edge, width - slots[i].Width - after)) }; edge = slots[i].Left + slots[i].Width; }
        }
        else {
            double edge = 0;
            for (int i = 0; i < slots.Length; i++) { slots[i] = slots[i] with { Left = Math.Max(slots[i].Left, edge) }; edge = slots[i].Left + slots[i].Width; }
            edge = width;
            for (int i = slots.Length - 1; i >= 0; i--) { slots[i] = slots[i] with { Left = Math.Min(slots[i].Left, edge - slots[i].Width) }; edge = slots[i].Left; }
        }
        return slots.ToList();
    }

    internal static void Save(Settings config, IReadOnlyList<DockSlot> slots, double width)
    {
        if (!double.IsFinite(width) || width <= 0) return;
        foreach (var slot in slots) {
            var item = config.TopDockItems.First(i => i.Id == slot.Id);
            item.Position = Math.Clamp(slot.Center / width, 0, 1);
            item.Group = item.Position < 1d / 3 ? "left" : item.Position > 2d / 3 ? "right" : "center";
        }
        // Match the controls list to the visible order without losing hidden buttons.
        var order = slots.Select((s, i) => (s.Id, Index: i)).ToDictionary(s => s.Id, s => s.Index);
        config.TopDockItems = config.TopDockItems.OrderBy(i => order.GetValueOrDefault(i.Id, int.MaxValue)).ToList();
    }
}

internal sealed class DockPlacementPanel : Panel
{
    readonly Settings config;
    readonly Dictionary<string, double> widths = new();
    double arrangedWidth;
    internal string? Moving { get; private set; }
    internal double? Position { get; private set; }
    internal IReadOnlyList<DockSlot> Slots { get; private set; } = Array.Empty<DockSlot>();
    internal double UsableWidth => Math.Max(arrangedWidth, widths.Values.Sum());
    internal DockPlacementPanel(Settings config) { this.config = config; Background = Brushes.Transparent; ClipToBounds = true; AllowDrop = true; }
    protected override Size MeasureOverride(Size availableSize)
    {
        widths.Clear(); double height = 0;
        foreach (FrameworkElement child in InternalChildren) { child.Measure(new Size(double.PositiveInfinity, availableSize.Height)); widths[(string)child.Tag] = child.DesiredSize.Width; height = Math.Max(height, child.DesiredSize.Height); }
        return new Size(widths.Values.Sum(), height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        arrangedWidth = finalSize.Width;
        Slots = Calculate(Moving, Position);
        var lookup = Slots.ToDictionary(s => s.Id);
        foreach (FrameworkElement child in InternalChildren) { var slot = lookup[(string)child.Tag]; child.Arrange(new Rect(slot.Left, 0, slot.Width, finalSize.Height)); }
        return finalSize;
    }
    internal List<DockSlot> Calculate(string? id = null, double? position = null) => DockPlacement.Arrange(config.TopDockItems, widths, UsableWidth, id, position);
    internal void Preview(string? id, double? position) { Moving = id; Position = position; InvalidateArrange(); UpdateLayout(); }
    internal bool Place(string id, double center)
    {
        if (!widths.ContainsKey(id) || !double.IsFinite(center) || UsableWidth <= 0) return false;
        var slots = Calculate(id, Math.Clamp(center / UsableWidth, 0, 1));
        DockPlacement.Save(config, slots, UsableWidth); Preview(null, null); return true;
    }
}
