using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Harbor;

try
{
    if (args.Contains("--install")) ShellSession.Install();
    else if (args.Contains("--restore"))
    {
        ShellSession.RestoreRegistration();
        if (File.Exists(ShellSession.AppPath))
        {
            using var restore = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ShellSession.AppPath, "--restore") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
            restore?.WaitForExit(10000);
        }
        ShellSession.EnsureExplorer();
    }
    else if (args.Contains("--stop")) ShellSession.RequestStop();
    else if (args.Contains("--status")) { }
    else ShellSession.RunHost(args.Contains("--login"));
    int output = Array.IndexOf(args, "--output");
    if (output >= 0 && args.Length > output + 1) File.WriteAllText(args[output + 1], JsonSerializer.Serialize(new { Installed = ShellSession.Enabled, Supervisor = ShellSession.HostRunning, ShellSession.BackupPath, ShellSession.ShellCommand }));
}
catch (Exception ex) { ShellSession.WriteLog(ex.ToString()); Environment.ExitCode = 1; }
