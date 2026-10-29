using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Harbor;

// Uses executable names and installed-game folders, never injection or GPU hooks.
internal sealed class Games
{
    readonly Settings config;
    readonly Dictionary<uint, (string Path, bool Game)> cache = new();
    static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        "RocketLeague", "Bodycam-Win64-Shipping", "RobloxPlayerBeta", "Minecraft.Windows",
        "FortniteClient-Win64-Shipping", "GTA5", "GTA5_Enhanced", "RDR2", "cs2", "dota2", "hl2",
        "GeometryDash", "VALORANT-Win64-Shipping", "League of Legends", "Overwatch", "r5apex",
        "cod", "aces", "Warframe.x64", "Brawlhalla", "FallGuys_client_game"
    };
    static readonly HashSet<string> Launchers = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "RiotClientServices",
        "RiotClientUx", "Battle.net", "Agent", "GalaxyClient", "UbisoftConnect", "EADesktop",
        "Unity", "Unity Hub", "UnrealEditor", "CrashReportClient", "java", "javaw"
    };
    internal IntPtr ActiveWindow { get; private set; }
    internal string ActivePath { get; private set; } = "";
    internal bool Active => ActiveWindow != IntPtr.Zero;
    internal Games(Settings config) => this.config = config;
    internal void Invalidate() => cache.Clear();
    internal bool IsGame(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (config.GameExclusions.Contains(path, StringComparer.OrdinalIgnoreCase)) return false;
        if (config.GameApps.Contains(path, StringComparer.OrdinalIgnoreCase)) return true;
        string name = Path.GetFileNameWithoutExtension(path);
        if (Launchers.Contains(name)) return false;
        if (Known.Contains(name)) return true;
        string normalized = path.Replace('/', '\\');
        return normalized.Contains(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Epic Games\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\XboxGames\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\GOG Games\", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase);
    }
    internal bool Update()
    {
        IntPtr active = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(active, out uint pid);
        if (pid == Environment.ProcessId || active == IntPtr.Zero || Native.IsIconic(active)) active = IntPtr.Zero;
        string cls = Native.Class(active);
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") active = IntPtr.Zero;
        string path = "";
        if (active != IntPtr.Zero)
        {
            if (!cache.TryGetValue(pid, out var entry))
            {
                path = Native.ProcessPath(pid);
                entry = (path, IsGame(path));
                if (cache.Count > 128) cache.Clear();
                if (path.Length > 0) cache[pid] = entry;
            }
            path = entry.Path;
            if (!entry.Game) active = IntPtr.Zero;
        }
        bool changed = active != ActiveWindow;
        ActiveWindow = active; ActivePath = active == IntPtr.Zero ? "" : path;
        if (changed) Log.Write(Active ? "Game focused: " + ActivePath : "Game focus ended.");
        return changed;
    }
}
