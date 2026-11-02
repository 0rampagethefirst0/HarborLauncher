using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace Harbor;

internal static class StartupApps
{
    // Explorer normally starts these. A replacement shell must keep enabled
    // startup apps working and respect the user's Startup Apps switches.
    internal static void Launch()
    {
        try
        {
            string logon = LogonIdentity();
            using var marker = Registry.CurrentUser.CreateSubKey(@"Software\Harbor\Session");
            if (marker.GetValue("StartupLogon") as string == logon) return;
            marker.SetValue("StartupLogon", logon);
            var started = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var run = root.OpenSubKey(ShellSession.RunKey); if (run == null) continue;
                    foreach (string name in run.GetValueNames())
                    {
                        if (name.Equals("Harbor", StringComparison.OrdinalIgnoreCase) || !Approved(root, "Run", name, view)) continue;
                        if (run.GetValue(name) is not string command || string.IsNullOrWhiteSpace(command) || !started.Add(command)) continue;
                        StartCommand(Environment.ExpandEnvironmentVariables(command));
                    }
                }
            foreach (string path in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
            {
                if (!Directory.Exists(path)) continue;
                foreach (string file in Directory.EnumerateFiles(path).Where(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!Approved(Registry.CurrentUser, "StartupFolder", Path.GetFileName(file), RegistryView.Registry64)) continue;
                    try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true })?.Dispose(); } catch (Exception ex) { ShellSession.WriteLog("Startup shortcut: " + ex.Message); }
                }
            }
            ShellSession.WriteLog("Enabled startup apps dispatched for this logon.");
        }
        catch (Exception ex) { ShellSession.WriteLog("Startup apps: " + ex.Message); }
    }
    static bool Approved(RegistryKey root, string group, string name, RegistryView view)
    {
        string location = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + (group == "Run" && view == RegistryView.Registry32 ? "Run32" : group);
        using var user = Registry.CurrentUser.OpenSubKey(location); using var key = root.OpenSubKey(location);
        var bytes = user?.GetValue(name) as byte[] ?? key?.GetValue(name) as byte[];
        return bytes == null || bytes.Length == 0 || bytes[0] is 0 or 2 or 6;
    }
    static string LogonIdentity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        IntPtr buffer = Marshal.AllocHGlobal(128);
        try
        {
            if (!GetTokenInformation(identity.AccessToken.DangerousGetHandle(), 10, buffer, 128, out _)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return Marshal.ReadInt64(buffer, 8).ToString("X16");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct StartInfo
    {
        internal int Size; internal string? Reserved, Desktop, Title; internal int X, Y, Width, Height, CharsX, CharsY, Fill, Flags; internal short Show, ReservedBytes; internal IntPtr ReservedPointer, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { internal IntPtr Process, Thread; internal uint Pid, ThreadId; }
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr buffer, int length, out int returned);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string? application, System.Text.StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string? directory, ref StartInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static void StartCommand(string command)
    {
        var startup = new StartInfo { Size = Marshal.SizeOf<StartInfo>() };
        if (!CreateProcess(null, new System.Text.StringBuilder(command), IntPtr.Zero, IntPtr.Zero, false, 0x08000000, IntPtr.Zero, null, ref startup, out var process)) { ShellSession.WriteLog("A startup app could not be launched: Windows error " + Marshal.GetLastWin32Error()); return; }
        CloseHandle(process.Process); CloseHandle(process.Thread);
    }
}
