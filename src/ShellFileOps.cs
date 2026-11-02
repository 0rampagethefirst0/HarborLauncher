using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Harbor;

// Use the Shell's file-operation engine for progress, collisions, permissions,
// cross-volume moves and session undo records. No Explorer window is opened.
internal static class ShellFileOps
{
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface Item
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetParent(out Item parent);
        void GetDisplayName(uint type, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(Item other, uint hint, out int order);
    }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface Operation
    {
        void Advise(IntPtr sink, out uint cookie); void Unadvise(uint cookie); void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message); void SetProgressDialog(IntPtr dialog); void SetProperties(IntPtr properties); void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(Item item); void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(Item item, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink); void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(Item item, Item destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr sink); void MoveItems(IntPtr items, Item destination);
        void CopyItem(Item item, Item destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr sink); void CopyItems(IntPtr items, Item destination);
        void DeleteItem(Item item, IntPtr sink); void DeleteItems(IntPtr items);
        void NewItem(Item destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, IntPtr sink);
        void PerformOperations(); void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out Item item);
    static Item Get(string path) { Guid id = typeof(Item).GUID; SHCreateItemFromParsingName(path, IntPtr.Zero, ref id, out var item); return item; }
    internal static Task TransferAsync(string[] paths, string destination, bool move, IntPtr owner, bool quiet = false) => Execute(owner, quiet, false, (op, keep) =>
    {
        var folder = Get(Path.GetFullPath(destination)); keep.Add(folder);
        foreach (string path in paths)
        {
            string from = Path.GetFullPath(path), to = Path.Combine(Path.GetFullPath(destination), Path.GetFileName(from.TrimEnd('\\')));
            if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) { if (move) continue; op.SetOperationFlags(Flags(quiet, false) | 8); }
            if (Directory.Exists(from) && (destination.TrimEnd('\\') + "\\").StartsWith(from.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("A folder cannot be copied or moved into itself.");
            var item = Get(from); keep.Add(item); if (move) op.MoveItem(item, folder, null, IntPtr.Zero); else op.CopyItem(item, folder, null, IntPtr.Zero);
        }
    });
    internal static Task RenameAsync(string path, string name, IntPtr owner, bool quiet = false) => Execute(owner, quiet, false, (op, keep) => { ValidateName(name); var item = Get(Path.GetFullPath(path)); keep.Add(item); op.RenameItem(item, name, IntPtr.Zero); });
    internal static Task RenameManyAsync(string[] paths, string name, IntPtr owner, bool quiet = false) => Execute(owner, quiet, false, (op, keep) => { ValidateName(name); int index = 1; foreach (string path in paths) { var item = Get(Path.GetFullPath(path)); keep.Add(item); op.RenameItem(item, name + " (" + index++ + ")" + (Directory.Exists(path) ? "" : Path.GetExtension(path)), IntPtr.Zero); } });
    internal static Task DeleteAsync(string[] paths, IntPtr owner, bool permanent = false, bool quiet = false) => Execute(owner, quiet, !permanent, (op, keep) => { foreach (string path in paths) { var item = Get(Path.GetFullPath(path)); keep.Add(item); op.DeleteItem(item, IntPtr.Zero); } });
    internal static Task NewFolderAsync(string folder, string name, IntPtr owner, bool quiet = false) => Execute(owner, quiet, false, (op, keep) => { ValidateName(name); var item = Get(Path.GetFullPath(folder)); keep.Add(item); op.NewItem(item, 0x10, name, null, IntPtr.Zero); });
    internal static Task NewTextAsync(string folder, string name, IntPtr owner, bool quiet = false) => Execute(owner, quiet, false, (op, keep) => { ValidateName(name); var item = Get(Path.GetFullPath(folder)); keep.Add(item); op.NewItem(item, 0x80, name, null, IntPtr.Zero); });
    internal static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ') || System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(name), "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new IOException("Choose a valid file or folder name."); }
    static uint Flags(bool quiet, bool recycle) => 0x200u | (recycle ? 0x80000u | 0x4000u : 0) | (quiet ? 0x4u | 0x10u | 0x400u | 0x100000u : 0x40u | 0x20000000u);
    static Task Execute(IntPtr owner, bool quiet, bool recycle, Action<Operation, List<object>> queue) => RecycleBin.Sta(() =>
    {
        var keep = new List<object>(); Operation? operation = null;
        try
        {
            operation = (Operation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"))!)!;
            operation.SetOwnerWindow(owner); operation.SetOperationFlags(Flags(quiet, recycle)); queue(operation, keep); operation.PerformOperations(); operation.GetAnyOperationsAborted(out bool aborted); if (aborted) throw new OperationCanceledException("File operation cancelled.");
        }
        finally { foreach (object value in keep) Marshal.FinalReleaseComObject(value); if (operation != null) Marshal.FinalReleaseComObject(operation); }
        return true;
    });
}
