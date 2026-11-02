using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Harbor;

internal static class ShellStartupVerification
{
    internal static void Run(Shell model, Action<string, bool, object?> check)
    {
        string path = @"Software\Harbor\Verification\" + Guid.NewGuid().ToString("N");
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(path)) { key.SetValue("Keep", "Other app"); key.SetValue("", "%TEMP%\\original.exe", RegistryValueKind.ExpandString); }
            var original = ShellSession.Capture(path, "");
            var absent = ShellSession.Capture(path, "Absent");
            using (var key = Registry.CurrentUser.CreateSubKey(path)) { key.SetValue("", "Harbor"); key.SetValue("Absent", "Temporary"); }
            ShellSession.RestoreValue(original); ShellSession.RestoreValue(absent);
            using var restored = Registry.CurrentUser.OpenSubKey(path)!;
            check("Shell rollback preserves original unexpanded registry strings and types", restored.GetValueKind("") == RegistryValueKind.ExpandString && (string?)restored.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == "%TEMP%\\original.exe", null);
            check("Shell rollback removes previously absent values without touching other values", !restored.GetValueNames().Contains("Absent") && (string?)restored.GetValue("Keep") == "Other app", null);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        var folder = new AppFolder { Name = "Popup regression" }; model.Config.AppFolders.Add(folder);
        var popup = new AppFolderPopup(model, folder, model.CurrentMonitor());
        int closes = 0;
        popup.Closing += (_, _) => { closes++; popup.RequestClose(); };
        popup.RequestClose(); popup.RequestClose();
        check("App-folder closing tolerates nested and repeated close requests", closes == 1, null);
        model.Config.AppFolders.Remove(folder);
        check("Sign-in shell command quotes the supervisor path and uses its login mode", ShellSession.ShellCommand.StartsWith("\"") && ShellSession.ShellCommand.EndsWith("\" --login"), null);
        check("Folder opening handles Windows' quoted drive-root command line", Program.NormalizeFolderArgument("C:\"") == "C:\\", null);
        check("Folder opening preserves spaces and Unicode paths", Program.NormalizeFolderArgument("C:\\Users\\My Files\\日本語") == "C:\\Users\\My Files\\日本語", null);
    }
}
