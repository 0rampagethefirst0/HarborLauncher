using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Harbor;

internal static class ShellFileMenu
{
    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Folder
    {
        void ParseDisplayName(IntPtr owner, IntPtr context, [MarshalAs(UnmanagedType.LPWStr)] string name, ref uint eaten, out IntPtr item, ref uint attributes);
        void EnumObjects(IntPtr owner, uint flags, out IntPtr enumerator);
        void BindToObject(IntPtr item, IntPtr context, ref Guid id, [MarshalAs(UnmanagedType.Interface)] out Folder folder);
        void BindToStorage(IntPtr item, IntPtr context, ref Guid id, out IntPtr storage);
        [PreserveSig] int CompareIDs(IntPtr flags, IntPtr a, IntPtr b);
        void CreateViewObject(IntPtr owner, ref Guid id, [MarshalAs(UnmanagedType.Interface)] out Menu menu);
        void GetAttributesOf(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] items, ref uint attributes);
        void GetUIObjectOf(IntPtr owner, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] items, ref Guid id, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out Menu menu);
        void GetDisplayNameOf(IntPtr item, uint flags, IntPtr name);
        void SetNameOf(IntPtr owner, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out IntPtr result);
    }
    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Menu
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        void InvokeCommand(ref Invoke info);
        [PreserveSig] int GetCommandString(UIntPtr command, uint flags, IntPtr reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, uint length);
    }
    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Menu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags); void InvokeCommand(ref Invoke info); [PreserveSig] int GetCommandString(UIntPtr command, uint flags, IntPtr reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, uint length);
        [PreserveSig] int HandleMenuMsg(uint message, IntPtr w, IntPtr l); [PreserveSig] int HandleMenuMsg2(uint message, IntPtr w, IntPtr l, out IntPtr result);
    }
    [StructLayout(LayoutKind.Sequential)] struct Invoke { public int Size; public uint Mask; public IntPtr Owner, Verb, Parameters, Directory; public int Show; public uint HotKey; public IntPtr Icon; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] static extern void SHParseDisplayName(string name, IntPtr context, out IntPtr item, uint flags, out uint attributes);
    [DllImport("shell32.dll", PreserveSig = false)] static extern void SHBindToParent(IntPtr item, ref Guid id, [MarshalAs(UnmanagedType.Interface)] out Folder folder, out IntPtr child);
    [DllImport("shell32.dll", PreserveSig = false)] static extern void SHGetDesktopFolder([MarshalAs(UnmanagedType.Interface)] out Folder folder);
    [DllImport("shell32.dll")] static extern IntPtr ILFindLastID(IntPtr item);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    internal static bool Show(Window owner, string[] paths, string folder) => Use(owner, paths, folder, null);
    internal static bool InvokeVerb(Window owner, string folder, string verb) => Use(owner, Array.Empty<string>(), folder, verb);
    static bool Use(Window window, string[] paths, string folderPath, string? canonical)
    {
        IntPtr owner = new WindowInteropHelper(window).Handle, nativeMenu = IntPtr.Zero; Folder? parent = null, desktop = null; Menu? menu = null; var allocations = new List<IntPtr>(); HwndSource? source = HwndSource.FromHwnd(owner); HwndSourceHook? hook = null;
        try
        {
            Guid folderId = typeof(Folder).GUID, menuId = typeof(Menu).GUID;
            if (paths.Length > 0)
            {
                string? common = System.IO.Path.GetDirectoryName(paths[0]); foreach (string path in paths) if (!string.Equals(common, System.IO.Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use the full menu with items from the same folder.");
                foreach (string path in paths) { SHParseDisplayName(path, IntPtr.Zero, out var item, 0, out _); allocations.Add(item); }
                SHBindToParent(allocations[0], ref folderId, out parent, out _); var children = allocations.ConvertAll(ILFindLastID).ToArray(); parent.GetUIObjectOf(owner, (uint)children.Length, children, ref menuId, IntPtr.Zero, out menu);
            }
            else
            {
                SHParseDisplayName(folderPath, IntPtr.Zero, out var item, 0, out _); allocations.Add(item); SHGetDesktopFolder(out desktop); desktop.BindToObject(item, IntPtr.Zero, ref folderId, out parent); parent.CreateViewObject(owner, ref menuId, out menu);
            }
            nativeMenu = CreatePopupMenu(); int commands = menu.QueryContextMenu(nativeMenu, 0, 1, 0x7FFF, 0x100); if (commands < 0) Marshal.ThrowExceptionForHR(commands);
            uint chosen = 0;
            if (canonical != null)
            {
                for (uint i = 0; i < (uint)(commands & 0xFFFF); i++) { var value = new StringBuilder(260); if (menu.GetCommandString(new UIntPtr(i), 4, IntPtr.Zero, value, 260) == 0 && value.ToString().Equals(canonical, StringComparison.OrdinalIgnoreCase)) { chosen = i + 1; break; } }
            }
            else
            {
                var extended = menu as Menu3;
                hook = (IntPtr h, int message, IntPtr w, IntPtr l, ref bool handled) => { if (extended != null && message is 0x117 or 0x2B or 0x2C or 0x120 && extended.HandleMenuMsg2((uint)message, w, l, out var result) == 0) { handled = true; return result; } return IntPtr.Zero; };
                source?.AddHook(hook); Native.GetCursorPos(out var cursor); chosen = TrackPopupMenuEx(nativeMenu, 0x102, cursor.X, cursor.Y, owner, IntPtr.Zero);
            }
            if (chosen == 0) return false;
            var invocation = new Invoke { Size = Marshal.SizeOf<Invoke>(), Owner = owner, Verb = new IntPtr(chosen - 1), Show = 1 }; menu.InvokeCommand(ref invocation); return true;
        }
        finally { if (hook != null) source?.RemoveHook(hook); if (nativeMenu != IntPtr.Zero) DestroyMenu(nativeMenu); if (menu != null) Marshal.FinalReleaseComObject(menu); if (parent != null) Marshal.FinalReleaseComObject(parent); if (desktop != null) Marshal.FinalReleaseComObject(desktop); foreach (var item in allocations) Marshal.FreeCoTaskMem(item); }
    }
}
