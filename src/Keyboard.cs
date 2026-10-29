using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Harbor;

internal sealed class Keyboard : IDisposable
{
    const ulong Marker = 0x484152424F52;
    delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct KeyData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? module);
    readonly HookProc callback;
    readonly IntPtr hook;
    readonly Dispatcher dispatcher;
    readonly Action open;
    readonly Action<uint> shortcut;
    readonly Action? typing;
    readonly HashSet<uint> consumed = new();
    bool left, right, forwarded, ctrlEscape, chord;
    internal Keyboard(Dispatcher dispatcher, Action open, Action<uint> shortcut, Action? typing = null)
    {
        this.dispatcher = dispatcher; this.open = open; this.shortcut = shortcut; this.typing = typing;
        callback = Handle;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        Log.Write(hook == IntPtr.Zero ? "Start-menu key hook unavailable; Ctrl+Alt+Space still works." : "Windows key and Ctrl+Esc launcher enabled.");
    }
    void Inject(byte key, bool up) => Native.keybd_event(key, 0, up ? 2u : 0, new UIntPtr(Marker));
    IntPtr Handle(int code, IntPtr message, IntPtr data)
    {
        if (code < 0) return CallNextHookEx(hook, code, message, data);
        var k = Marshal.PtrToStructure<KeyData>(data);
        if (k.Extra.ToUInt64() == Marker) return CallNextHookEx(hook, code, message, data);
        bool down = message.ToInt32() is 0x100 or 0x104;
        bool up = message.ToInt32() is 0x101 or 0x105;
        if (down && !left && !right && DockTyping.TextKey(k.Key) && (GetAsyncKeyState(0x12) & 0x8000) == 0) { try { typing?.Invoke(); } catch { } }
        if (consumed.Contains(k.Key)) { if (up) consumed.Remove(k.Key); return new IntPtr(1); }
        if (k.Key == 0x1B)
        {
            // Consume both Escape events so Explorer never receives the Start shortcut.
            // Ctrl itself stays untouched, including when it is released before Escape.
            if (down && (ctrlEscape ||
                ((GetAsyncKeyState(0x11) & 0x8000) != 0 &&
                 (GetAsyncKeyState(0x10) & 0x8000) == 0 &&
                 (GetAsyncKeyState(0x12) & 0x8000) == 0 && !left && !right)))
            {
                ctrlEscape = true;
                return new IntPtr(1);
            }
            if (up && ctrlEscape)
            {
                ctrlEscape = false;
                dispatcher.BeginInvoke(open);
                return new IntPtr(1);
            }
        }
        if (k.Key is 0x5B or 0x5C)
        {
            if (down)
            {
                if (k.Key == 0x5B) { if (!left) { left = true; if (forwarded) Inject(0x5B, false); } }
                else if (!right) { right = true; if (forwarded) Inject(0x5C, false); }
            }
            else if (up)
            {
                bool wasDown = k.Key == 0x5B ? left : right;
                if (forwarded) Inject((byte)k.Key, true);
                if (k.Key == 0x5B) left = false; else right = false;
                if (wasDown && !forwarded && !chord) dispatcher.BeginInvoke(open);
                if (!left && !right) { forwarded = false; chord = false; }
            }
            return new IntPtr(1);
        }
        if (down && (left || right) && !forwarded)
        {
            if (k.Key is 0x45 or 0x44 or 0x4E or 0x41 && (GetAsyncKeyState(0x11) & 0x8000) == 0 && (GetAsyncKeyState(0x10) & 0x8000) == 0 && (GetAsyncKeyState(0x12) & 0x8000) == 0)
            {
                chord = true; consumed.Add(k.Key); dispatcher.BeginInvoke(new Action(() => shortcut(k.Key))); return new IntPtr(1);
            }
            forwarded = true;
            if (left) Inject(0x5B, false);
            if (right) Inject(0x5C, false);
        }
        return CallNextHookEx(hook, code, message, data);
    }
    public void Dispose()
    {
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        if (forwarded) { if (left) Inject(0x5B, true); if (right) Inject(0x5C, true); }
        left = right = forwarded = ctrlEscape = chord = false; consumed.Clear();
    }
}
