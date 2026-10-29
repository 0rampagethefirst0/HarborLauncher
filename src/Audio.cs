using System;
using System.Runtime.InteropServices;

namespace Harbor;

internal static partial class Audio
{
    internal static (float Volume, bool Muted) Read()
    {
        float volume = 0; bool muted = false;
        With(v => { v.GetMasterVolumeLevelScalar(out volume); v.GetMute(out muted); });
        return (volume, muted);
    }
    internal static void SetVolume(float value) => With(v => v.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), Guid.Empty));
    internal static void SetMute(bool value) => With(v => v.SetMute(value, Guid.Empty));
    static void With(Action<IVolume> action)
    {
        IDeviceEnumerator? enumerator = null; IDevice? device = null; object? volume = null;
        try
        {
            enumerator = (IDeviceEnumerator)new DeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
            var guid = typeof(IVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref guid, 23, IntPtr.Zero, out volume));
            action((IVolume)volume);
        }
        catch (Exception ex) { Log.Write("System audio: " + ex.Message); }
        finally { foreach (var obj in new[] { volume, device, enumerator }) if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
    }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out IDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice
    {
        [PreserveSig] int Activate(ref Guid guid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IVolume
    {
        int RegisterControlChangeNotify(IntPtr notify); int UnregisterControlChangeNotify(IntPtr notify); int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level, Guid context); int SetMasterVolumeLevelScalar(float level, Guid context);
        int GetMasterVolumeLevel(out float level); int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, Guid context); int SetChannelVolumeLevelScalar(uint channel, float level, Guid context);
        int GetChannelVolumeLevel(uint channel, out float level); int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid context); int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        int GetVolumeStepInfo(out uint step, out uint count); int VolumeStepUp(Guid context); int VolumeStepDown(Guid context);
        int QueryHardwareSupport(out uint mask); int GetVolumeRange(out float min, out float max, out float step);
    }
}
