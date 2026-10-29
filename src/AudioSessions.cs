using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Harbor;

internal sealed record AppAudioSession(uint ProcessId, string Path, float Volume, bool Muted);

internal static partial class Audio
{
    internal static string SessionError { get; private set; } = "";
    internal static List<AppAudioSession> Sessions()
    {
        var result = new List<AppAudioSession>();
        WithSessions((pid, path, volume) => { Marshal.ThrowExceptionForHR(volume.GetMasterVolume(out float level)); Marshal.ThrowExceptionForHR(volume.GetMute(out bool muted)); result.Add(new(pid, path, level, muted)); });
        return result;
    }
    internal static int AdjustApp(IReadOnlyCollection<uint> pids, IReadOnlyCollection<string> paths, float delta)
    {
        int changed = 0;
        WithSessions((pid, path, volume) =>
        {
            if (!pids.Contains(pid) && (path.Length == 0 || !paths.Contains(path, StringComparer.OrdinalIgnoreCase))) return;
            Marshal.ThrowExceptionForHR(volume.GetMasterVolume(out float level)); var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(volume.SetMasterVolume(Math.Clamp(level + delta, 0, 1), ref context)); changed++;
        });
        return changed;
    }
    static void Release(object? obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
    static void WithSessions(Action<uint, string, ISimpleVolume> action)
    {
        IDeviceEnumerator? enumerator = null; IDeviceCollection? devices = null; SessionError = "";
        int com = Native.CoInitializeEx(IntPtr.Zero, 0);
        try
        {
            enumerator = (IDeviceEnumerator)new DeviceEnumerator(); Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 1, out devices)); Marshal.ThrowExceptionForHR(devices.GetCount(out uint count));
            for (uint i = 0; i < Math.Min(count, 100); i++)
            {
                IDevice? device = null; object? managerObject = null; ISessionEnumerator? sessions = null;
                try
                {
                    Marshal.ThrowExceptionForHR(devices.Item(i, out device)); var id = typeof(ISessionManager).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out managerObject));
                    var manager = (ISessionManager)managerObject; Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessions)); Marshal.ThrowExceptionForHR(sessions.GetCount(out int total));
                    for (int j = 0; j < Math.Min(total, 1000); j++)
                    {
                        object? session = null;
                        try
                        {
                            Marshal.ThrowExceptionForHR(sessions.GetSession(j, out session));
                            if (session is not ISessionControl control || session is not ISimpleVolume volume) continue;
                            Marshal.ThrowExceptionForHR(control.GetProcessId(out uint pid)); Marshal.ThrowExceptionForHR(control.GetState(out int state));
                            if (pid == 0 || state == 2 || control.IsSystemSoundsSession() == 0) continue;
                            action(pid, Native.ProcessPath(pid), volume);
                        }
                        finally { Release(session); }
                    }
                }
                catch (Exception ex) { SessionError = ex.Message; }
                finally { Release(sessions); Release(managerObject); Release(device); }
            }
        }
        catch (Exception ex) { SessionError = ex.Message; }
        finally { Release(devices); Release(enumerator); if (com >= 0) Native.CoUninitialize(); }
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index, out IDevice device);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionManager
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid id, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid id, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IntPtr notify); [PreserveSig] int UnregisterSessionNotification(IntPtr notify);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr notify); [PreserveSig] int UnregisterDuckNotification(IntPtr notify);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionEnumerator
    {
        [PreserveSig] int GetCount(out int count); [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionControl
    {
        [PreserveSig] int GetState(out int state); [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name); [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path); [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping); [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterNotification(IntPtr notify); [PreserveSig] int UnregisterNotification(IntPtr notify);
        [PreserveSig] int GetIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id); [PreserveSig] int GetInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid); [PreserveSig] int IsSystemSoundsSession(); [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISimpleVolume
    {
        [PreserveSig] int SetMasterVolume(float value, ref Guid context); [PreserveSig] int GetMasterVolume(out float value);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool value, ref Guid context); [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool value);
    }
}
