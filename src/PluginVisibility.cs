using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Harbor;

internal static class PluginVisibility
{
    internal const string Always = "Always available", Apps = "Apps screen only", Selected = "With chosen apps";
    internal static readonly string[] Modes = { Always, Apps, Selected };
    internal static string Mode(Mods mods, ModDefinition mod) => Modes.Contains(mods.Value(mod, "visibility")) ? mods.Value(mod, "visibility") : Always;
    internal static string[] Rules(Mods mods, ModDefinition mod) => mods.Value(mod, "visible-apps").Split(new[] { '\n', '\r' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    internal static bool Allowed(Shell shell, ModDefinition mod) => Evaluate(Mode(shell.Mods, mod), shell.AppsOpen, Rules(shell.Mods, mod), shell.Tracker.Windows);
    internal static bool Evaluate(string mode, bool appsOpen, IEnumerable<string> rules, IEnumerable<WindowEntry> windows) => mode switch { Apps => appsOpen, Selected => windows.Any(w => rules.Any(r => Matches(r, w))), _ => true };
    internal static bool Matches(string rule, WindowEntry window)
    {
        rule = rule.Trim().Trim('"'); if (rule.Length == 0 || rule.Length > 1000) return false;
        const string prefix = @"shell:AppsFolder\";
        if (rule.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) rule = rule[prefix.Length..];
        string name = window.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? window.ProcessName : window.ProcessName + ".exe";
        if (!rule.Contains('*') && !rule.Contains('?')) return new[] { window.Key, window.Path, window.AppId, window.ProcessName, name }.Any(v => v.Equals(rule, StringComparison.OrdinalIgnoreCase)) || Shell.KeysMatch(window.Key, rule) || Shell.KeysMatch(window.Path, rule);
        string pattern = "^" + Regex.Escape(rule).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        try { return new[] { window.Key, window.Path, window.AppId, window.ProcessName, name }.Any(v => Regex.IsMatch(v, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20))); }
        catch (RegexMatchTimeoutException) { return false; }
    }
    internal static string Summary(Shell shell, ModDefinition mod)
    {
        string mode = Mode(shell.Mods, mod); int count = Rules(shell.Mods, mod).Length;
        return mode == Selected ? count == 0 ? "Choose apps to start this rule" : "Visible while any of " + count + " chosen app" + (count == 1 ? " is" : "s are") + " open" : mode == Apps ? "Visible while Harbor's Apps screen is open" : "Available even when the Apps screen is closed";
    }
    internal static string RuleName(string rule) { try { return rule.Contains('!') ? rule : Path.GetFileName(rule); } catch { return rule; } }
    internal static string AppKey(AppEntry app)
    {
        if (AppIdentity.Packaged(app.Key)) return AppIdentity.Normalize(app.Key);
        string file = Path.GetFileName(app.Key);
        return file.Equals("Update.exe", StringComparison.OrdinalIgnoreCase) || file.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase) ? new string(app.Name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()) + ".exe" : file;
    }
    internal static string AppName(string name, string key)
    {
        if (AppIdentity.Packaged(key)) return name;
        string executable = Path.GetFileNameWithoutExtension(key), simple = new(name.Where(char.IsLetterOrDigit).ToArray());
        return simple.Contains(new string(executable.Where(char.IsLetterOrDigit).ToArray()), StringComparison.OrdinalIgnoreCase) ? name : executable;
    }
}
