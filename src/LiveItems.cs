using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace Harbor;

internal static class LiveItems
{
    internal static void Set<T>(ItemsControl view, IReadOnlyList<T> desired, Func<T, string> key, bool keep)
    {
        if (!keep || view.ItemsSource is not ObservableCollection<T> current) { view.ItemsSource = new ObservableCollection<T>(desired); return; }
        var keys = desired.Select(key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = current.Count - 1; i >= 0; i--) if (!keys.Contains(key(current[i]))) current.RemoveAt(i);
        for (int i = 0; i < desired.Count; i++)
        {
            string next = key(desired[i]);
            if (i >= current.Count || !key(current[i]).Equals(next, StringComparison.OrdinalIgnoreCase))
            {
                int at = -1; for (int j = i + 1; j < current.Count; j++) if (key(current[j]).Equals(next, StringComparison.OrdinalIgnoreCase)) { at = j; break; }
                if (at >= 0) current.Move(at, i); else current.Insert(i, desired[i]);
            }
            if (!EqualityComparer<T>.Default.Equals(current[i], desired[i])) current[i] = desired[i];
        }
    }
}
