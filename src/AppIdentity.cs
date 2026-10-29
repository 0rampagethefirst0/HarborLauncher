using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Harbor;

internal static class AppIdentity
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)] struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out Variant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref Variant value);
        [PreserveSig] int Commit();
    }
    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr window, ref Guid iid, out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder? id);
    static string Read(IPropertyStore store)
    {
        var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
        if (store.GetValue(ref key, out var value) < 0) return "";
        try { return value.Type is 31 or 8 && value.Pointer != IntPtr.Zero ? Marshal.PtrToStringUni(value.Pointer) ?? "" : ""; }
        finally { PropVariantClear(ref value); }
    }
    internal static string LinkId(object link) { try { return Read((IPropertyStore)link); } catch { return ""; } }
    internal static string WindowId(IntPtr window)
    {
        IPropertyStore? store = null;
        try { var iid = typeof(IPropertyStore).GUID; return SHGetPropertyStoreForWindow(window, ref iid, out store) >= 0 ? Read(store) : ""; }
        catch { return ""; }
        finally { if (store != null) Marshal.ReleaseComObject(store); }
    }
    internal static void SetOwnWindowId(IntPtr window, string id)
    {
        Native.GetWindowThreadProcessId(window, out uint owner); if (owner != Environment.ProcessId) throw new InvalidOperationException("Window identity can only be assigned to Harbor windows.");
        var iid = typeof(IPropertyStore).GUID; Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window, ref iid, out var store));
        var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 }; var value = id.Length == 0 ? new Variant() : new Variant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(id) };
        try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value)); } finally { PropVariantClear(ref value); Marshal.ReleaseComObject(store); }
    }
    internal static string ProcessId(uint pid)
    {
        var process = OpenProcess(0x1000, false, pid); if (process == IntPtr.Zero) return "";
        try { uint length = 0; if (GetApplicationUserModelId(process, ref length, null) != 122 || length == 0 || length > 4096) return ""; var id = new StringBuilder((int)length); return GetApplicationUserModelId(process, ref length, id) == 0 ? id.ToString() : ""; }
        finally { CloseHandle(process); }
    }
    internal static (string Id, string Path) ForWindow(IntPtr window, uint pid, string path)
    {
        string id = WindowId(window);
        if (Native.Class(window) == "ApplicationFrameWindow")
        {
            Native.EnumChildWindows(window, (child, _) =>
            {
                Native.GetWindowThreadProcessId(child, out uint childPid); if (childPid == pid || childPid == 0) return true;
                string childId = ProcessId(childPid); if (!Packaged(childId)) return true;
                if (!Packaged(id)) id = childId; string childPath = Native.ProcessPath(childPid); if (childPath.Length > 0) path = childPath; return false;
            }, IntPtr.Zero);
        }
        if (!Packaged(id)) { string processId = ProcessId(pid); if (Packaged(processId)) id = processId; }
        return (Packaged(id) ? id : "", path);
    }
    internal static bool Packaged(string id) => !string.IsNullOrWhiteSpace(id) && id.Contains('!');
    internal static string Normalize(string key) => key.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase) ? key[17..] : key;
}
