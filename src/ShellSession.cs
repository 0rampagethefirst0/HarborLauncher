using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

namespace Harbor;

// Shared with the small sign-in supervisor. It has no UI/framework dependencies.
internal static class ShellSession
{
    internal const string Winlogon = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string TaskName = "Harbor Desktop";
    internal static string DataDir { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Harbor");
    internal static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
    internal static string AppPath => Path.Combine(Root, "App", "Harbor.exe");
    internal static string HostPath => Path.Combine(Root, "Session", "HarborSession.exe");
    internal static string StatePath => Path.Combine(DataDir, "shell-install.json");
    internal static bool IsShellProcess { get; set; }
    static string UserId => WindowsIdentity.GetCurrent().User!.Value;
    static string MutexName => @"Local\Harbor.Session." + UserId;
    static string StopName => @"Local\Harbor.Session.Stop." + UserId;
    internal static string BackupPath => ReadState()?.BackupDirectory ?? "";
    internal static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(Winlogon); return string.Equals(key?.GetValue("Shell") as string, ShellCommand, StringComparison.OrdinalIgnoreCase); }
    }
    internal static string ShellCommand => "\"" + HostPath + "\" --login";
    internal static bool HostRunning { get { try { using var mutex = Mutex.OpenExisting(MutexName); return true; } catch { return false; } } }
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static void Save(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, text); File.Move(temporary, path, true);
    }
    internal static void WriteLog(string message)
    {
        try { Directory.CreateDirectory(DataDir); File.AppendAllText(Path.Combine(DataDir, "Session.log"), DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine); } catch { }
    }
    internal static void Install()
    {
        if (!File.Exists(AppPath) || !File.Exists(HostPath)) throw new FileNotFoundException("Build and install both Harbor and its sign-in supervisor first.");
        Directory.CreateDirectory(DataDir);
        var state = ReadState();
        if (state == null && File.Exists(StatePath)) throw new InvalidDataException("The shell backup record cannot be read. Use its independent Restore Windows.ps1 before reinstalling.");
        if (state == null)
        {
            state = new ShellInstallState { BackupDirectory = Path.Combine(DataDir, "ShellBackups", DateTime.Now.ToString("yyyyMMdd-HHmmss")), InstalledUtc = DateTime.UtcNow };
            state.Values.Add(Capture(Winlogon, "Shell"));
            state.Values.Add(Capture(RunKey, "Harbor"));
            foreach (string kind in new[] { "Directory", "Drive" })
            {
                string key = @"Software\Classes\" + kind + @"\shell";
                state.Values.Add(Capture(key, ""));
                state.Values.Add(Capture(key + @"\Harbor.Open", ""));
                state.Values.Add(Capture(key + @"\Harbor.Open\command", ""));
            }
            Directory.CreateDirectory(state.BackupDirectory);
            string snapshot = JsonSerializer.Serialize(state, Json);
            Save(Path.Combine(state.BackupDirectory, "registry-values.json"), snapshot);
            Save(Path.Combine(state.BackupDirectory, "Restore Windows.ps1"), RestoreScript);
            Save(StatePath, snapshot); // Commit the backup before changing the first value.
        }
        try
        {
            RegisterTask();
            foreach (string kind in new[] { "Directory", "Drive" })
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + kind + @"\shell");
                using var verb = key.CreateSubKey("Harbor.Open"); verb.SetValue("", "Open in Harbor Files");
                using var command = verb.CreateSubKey("command"); command.SetValue("", "\"" + AppPath + "\" --open-folder \"%1\"");
                key.SetValue("", "Harbor.Open");
            }
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey)) run.DeleteValue("Harbor", false);
            using (var winlogon = Registry.CurrentUser.CreateSubKey(Winlogon)) winlogon.SetValue("Shell", ShellCommand);
            NotifyAssociations();
            WriteLog("Installed sign-in shell and supervised logon task. Backup: " + state.BackupDirectory);
        }
        catch { RestoreRegistration(false); throw; }
    }
    internal static void RestoreRegistration(bool disableOldAutostart = true)
    {
        var state = ReadState();
        if (state != null)
        {
            foreach (var value in state.Values) RestoreValue(value);
            // Returning to Windows also turns off Harbor's old overlay autostart.
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            if (disableOldAutostart && run.GetValue("Harbor") is string command && command.Contains("Harbor.exe", StringComparison.OrdinalIgnoreCase)) run.DeleteValue("Harbor", false);
            Save(Path.Combine(state.BackupDirectory, "restored.txt"), DateTimeOffset.Now.ToString("O"));
            File.Delete(StatePath);
        }
        else
        {
            using var winlogon = Registry.CurrentUser.OpenSubKey(Winlogon, true);
            if (winlogon != null && (winlogon.GetValue("Shell") as string)?.Contains("HarborSession.exe", StringComparison.OrdinalIgnoreCase) == true) winlogon.DeleteValue("Shell", false);
        }
        RemoveTask(); NotifyAssociations(); RequestStop(); WriteLog("Restored original desktop shell registration.");
    }
    internal static RegistryValueBackup Capture(string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        bool exists = key?.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase) == true;
        var result = new RegistryValueBackup { Path = path, Name = name, Exists = exists };
        if (exists) { result.Kind = key!.GetValueKind(name); result.Value = (string?)key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames); }
        return result;
    }
    internal static void RestoreValue(RegistryValueBackup saved)
    {
        using var key = Registry.CurrentUser.CreateSubKey(saved.Path);
        if (saved.Exists) key.SetValue(saved.Name, saved.Value ?? "", saved.Kind);
        else key.DeleteValue(saved.Name, false);
    }
    static ShellInstallState? ReadState() { try { return JsonSerializer.Deserialize<ShellInstallState>(File.ReadAllText(StatePath)); } catch { return null; } }
    static object TaskService()
    {
        var service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")! )!;
        ((dynamic)service).Connect(); return service;
    }
    static void RegisterTask()
    {
        dynamic service = TaskService();
        try
        {
            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Description = "Starts Harbor as the desktop and recovers after an unexpected crash.";
            definition.Principal.UserId = UserId; definition.Principal.LogonType = 3; definition.Principal.RunLevel = 0;
            definition.Settings.Enabled = true; definition.Settings.StartWhenAvailable = true;
            definition.Settings.DisallowStartIfOnBatteries = false; definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S"; definition.Settings.MultipleInstances = 2;
            dynamic trigger = definition.Triggers.Create(9); trigger.UserId = UserId; trigger.Enabled = true;
            dynamic action = definition.Actions.Create(0); action.Path = HostPath; action.Arguments = "--login"; action.WorkingDirectory = Path.GetDirectoryName(HostPath);
            service.GetFolder("\\").RegisterTaskDefinition(TaskName, definition, 6, UserId, null, 3, null);
        }
        finally { Marshal.FinalReleaseComObject(service); }
    }
    static void RemoveTask()
    {
        try { dynamic service = TaskService(); try { service.GetFolder("\\").DeleteTask(TaskName, 0); } finally { Marshal.FinalReleaseComObject(service); } } catch (Exception ex) { WriteLog("Remove startup task: " + ex.Message); }
    }
    internal static void RequestStop() { try { using var signal = EventWaitHandle.OpenExisting(StopName); signal.Set(); } catch { } }
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint message, IntPtr w, IntPtr l);
    delegate bool WindowVisitor(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr window);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint kind, uint flags, IntPtr first, IntPtr second);
    static void NotifyAssociations() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    internal static void EnsureExplorer()
    {
        var desktop = GetShellWindow(); bool hung = false;
        EnumWindows((h, _) => { if (WindowClass(h) == "Shell_TrayWnd" && IsHungAppWindow(h)) hung = true; return true; }, IntPtr.Zero);
        if (desktop != IntPtr.Zero && !hung) return;
        // A partially exited Explorer can retain shell COM/IPC endpoints and
        // deadlock its replacement. Clear that process before restoring it.
        CloseExplorerProcesses(false);
        try { Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true })?.Dispose(); } catch (Exception ex) { WriteLog("Explorer recovery: " + ex.Message); }
    }
    static string WindowClass(IntPtr window) { var name = new System.Text.StringBuilder(256); GetClassName(window, name, name.Capacity); return name.ToString(); }
    static bool CloseExplorerProcesses(bool normalExit)
    {
        var processes = new List<Process>();
        try
        {
            int session = Process.GetCurrentProcess().SessionId;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try { if (process.SessionId == session && string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase)) { processes.Add(process); continue; } } catch { }
                process.Dispose();
            }
            bool fileOperation = false;
            EnumWindows((h, _) => { GetWindowThreadProcessId(h, out uint pid); if (processes.Any(p => p.Id == pid) && WindowClass(h) == "OperationStatusWindow") fileOperation = true; return true; }, IntPtr.Zero);
            if (fileOperation) { WriteLog("Explorer handoff deferred until its file operation completes."); return false; }
            if (normalExit)
            {
                var window = GetShellWindow();
                if (window != IntPtr.Zero) { uint thread = GetWindowThreadProcessId(window, out _); PostThreadMessage(thread, 0x12, IntPtr.Zero, IntPtr.Zero); Thread.Sleep(500); }
            }
            foreach (var process in processes) if (!process.HasExited) { process.Kill(); process.WaitForExit(1500); }
            return true;
        }
        catch (Exception ex) { WriteLog("Explorer desktop handoff: " + ex.Message); return false; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
    internal static void RunHost(bool login)
    {
        using var mutex = new Mutex(true, MutexName, out bool first); if (!first) return;
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName);
        bool noExplorerAtLogin = login && GetShellWindow() == IntPtr.Zero;
        int crashes = 0; var window = Stopwatch.StartNew();
        WriteLog("Session supervisor started.");
        try
        {
            while (!stop.WaitOne(0) && Enabled)
            {
                var start = new ProcessStartInfo(AppPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(AppPath)!, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                start.Environment.Remove("ELECTRON_RUN_AS_NODE"); start.ArgumentList.Add("--shell-session");
                using var child = Process.Start(start) ?? throw new IOException("Harbor could not be started.");
                bool ready = false, explorerClosed = false;
                var startup = Stopwatch.StartNew();
                while (!child.WaitForExit(500) && !stop.WaitOne(0))
                {
                    if (!ready && Ready(child.Id))
                    {
                        ready = true;
                        if (noExplorerAtLogin) { noExplorerAtLogin = false; _ = System.Threading.Tasks.Task.Run(StartupApps.Launch); }
                        WriteLog("Harbor desktop ready, PID " + child.Id);
                    }
                    if (ready && !explorerClosed) explorerClosed = CloseExplorerProcesses(true);
                    if (!ready && startup.Elapsed > TimeSpan.FromSeconds(45)) { WriteLog("Startup timed out; terminating only the unready Harbor process."); child.Kill(); child.WaitForExit(5000); break; }
                }
                if (stop.WaitOne(0)) break;
                int code = child.HasExited ? child.ExitCode : -1;
                if (code == 0 && ready) { WriteLog("Harbor exited normally; returning to Windows for this session."); EnsureExplorer(); break; }
                if (window.Elapsed > TimeSpan.FromMinutes(5)) { crashes = 0; window.Restart(); }
                if (++crashes >= 3) { WriteLog("Repeated startup/crash failure; reverting shell registration."); RestoreRegistration(); EnsureExplorer(); break; }
                WriteLog("Restarting Harbor after unexpected exit " + code + ". Attempt " + crashes);
                if (stop.WaitOne(1800)) break;
            }
        }
        catch (Exception ex) { WriteLog("Session failure: " + ex); RestoreRegistration(); EnsureExplorer(); }
        WriteLog("Session supervisor stopped.");
    }
    static bool Ready(int pid)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDir, "recovery.json")));
            var root = json.RootElement;
            return root.GetProperty("ProcessId").GetInt32() == pid && root.TryGetProperty("ShellReady", out var value) && value.GetBoolean();
        }
        catch { return false; }
    }
    const string RestoreScript = """
        $ErrorActionPreference = 'Stop'
        $saved = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'registry-values.json') -Raw | ConvertFrom-Json
        foreach ($value in $saved.Values) {
            $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($value.Path)
            try {
                if ($value.Exists) { $key.SetValue($value.Name, [string]$value.Value, [Microsoft.Win32.RegistryValueKind][int]$value.Kind) }
                else { $key.DeleteValue($value.Name, $false) }
            } finally { $key.Dispose() }
        }
        $run = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
        try { if ([string]$run.GetValue('Harbor') -like '*Harbor.exe*') { $run.DeleteValue('Harbor', $false) } } finally { $run.Dispose() }
        Unregister-ScheduledTask -TaskName 'Harbor Desktop' -Confirm:$false -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'Harbor\shell-install.json') -ErrorAction SilentlyContinue
        try { $event = [Threading.EventWaitHandle]::OpenExisting('Local\Harbor.Session.Stop.' + [Security.Principal.WindowsIdentity]::GetCurrent().User.Value); $event.Set() | Out-Null; $event.Dispose() } catch {}
        $app = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Harbor\App\Harbor.exe'
        if (Test-Path -LiteralPath $app) { $restore = Start-Process -FilePath $app -ArgumentList '--restore' -WindowStyle Hidden -PassThru; $restore.WaitForExit(10000) | Out-Null }
        if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
        Write-Output 'Windows desktop restored. Harbor will no longer replace it at sign-in.'
        """;
}
internal sealed class ShellInstallState
{
    public DateTime InstalledUtc { get; set; }
    public string BackupDirectory { get; set; } = "";
    public List<RegistryValueBackup> Values { get; set; } = new();
}
internal sealed class RegistryValueBackup
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Exists { get; set; }
    public RegistryValueKind Kind { get; set; } = RegistryValueKind.String;
    public string? Value { get; set; }
}
