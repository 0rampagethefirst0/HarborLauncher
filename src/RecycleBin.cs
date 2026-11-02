using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Harbor;

internal sealed record RecycledItem(string Path, string Name, string OriginalFolder, DateTime Deleted, long Bytes, bool Folder)
{
    public string DisplaySize => Folder ? "Folder" : FileTools.Size(Bytes);
}
internal static class RecycleBin
{
    internal const string Location = "harbor-recycle://";
    [StructLayout(LayoutKind.Sequential)] struct Info { public uint Size; public long Bytes, Items; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBin(string? root, ref Info info);
    internal static (long Items, long Bytes) Query() { var info = new Info { Size = (uint)Marshal.SizeOf<Info>() }; int hr = SHQueryRecycleBin(null, ref info); return hr == 0 ? (info.Items, info.Bytes) : (0, 0); }
    internal static Task<T> Sta<T>(Func<T> work)
    {
        var promise = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { promise.SetResult(work()); } catch (Exception ex) { promise.SetException(ex); } }) { IsBackground = true, Name = "Harbor shell items" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return promise.Task;
    }
    internal static Task<List<RecycledItem>> ReadAsync() => Sta(() =>
    {
        dynamic? shell = null, folder = null, items = null; var result = new List<RecycledItem>();
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!); folder = shell!.NameSpace(10); items = folder.Items();
            for (int i = 0; i < (int)items.Count; i++)
            {
                dynamic item = items.Item(i);
                try
                {
                    string name = (string)item.Name, path = (string)item.Path;
                    string original = Convert.ToString(item.ExtendedProperty("System.Recycle.DeletedFrom")) ?? "";
                    object? deleted = item.ExtendedProperty("System.Recycle.DateDeleted"); DateTime when = deleted is DateTime d ? d : DateTime.MinValue;
                    object? size = item.ExtendedProperty("System.Size"); long bytes = size == null ? 0 : Convert.ToInt64(size);
                    result.Add(new RecycledItem(path, name, original, when, bytes, (bool)item.IsFolder));
                }
                catch (COMException) { }
                finally { Release(item); }
            }
        }
        finally { Release(items); Release(folder); Release(shell); }
        return result.OrderByDescending(i => i.Deleted).ToList();
    });
    internal static Task RestoreAsync(IEnumerable<string> paths) => Sta(() =>
    {
        var selected = paths.ToHashSet(StringComparer.OrdinalIgnoreCase); dynamic? shell = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!); folder = shell!.NameSpace(10); items = folder.Items();
            for (int i = (int)items.Count - 1; i >= 0; i--) { dynamic item = items.Item(i); try { if (selected.Contains((string)item.Path)) item.InvokeVerb("undelete"); } finally { Release(item); } }
        }
        finally { Release(items); Release(folder); Release(shell); }
        return true;
    });
    static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
