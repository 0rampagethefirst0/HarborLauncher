using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Harbor;

public sealed class AppEntry
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Target { get; init; } = "";
    public string Key => string.IsNullOrWhiteSpace(Target) ? Path : Target;
    public Pin ToPin() => new() { Name = Name, Path = Path, Target = Target };
}

internal sealed class Catalog
{
    internal List<AppEntry> Apps { get; private set; } = new();
    internal bool Ready { get; private set; }
    internal event Action? Changed;
    internal void Start()
    {
        var thread = new Thread(() =>
        {
            var apps = new List<AppEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] roots = {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            foreach (var root in roots)
            {
                try
                {
                    foreach (string file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                    {
                        string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                        if (ext is not ".lnk" and not ".url" and not ".appref-ms") continue;
                        string name = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!seen.Add(name)) continue;
                        apps.Add(new AppEntry { Name = name, Path = file, Target = ext == ".lnk" ? Resolve(file) : file });
                    }
                }
                catch (Exception ex) { Log.Write("App index: " + ex.Message); }
            }
            // Packaged Windows apps often have no Start-menu shortcut file.
            object? shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
                folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
                items = ((dynamic)folder!).Items();
                foreach (dynamic item in (dynamic)items!)
                {
                    try
                    {
                        string name = item.Name; string id = item.Path;
                        if (seen.Add(name)) apps.Add(new AppEntry { Name = name, Path = @"shell:AppsFolder\" + id, Target = id });
                    }
                    catch { }
                    finally { if (Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }
                }
            }
            catch (Exception ex) { Log.Write("Packaged apps: " + ex.Message); }
            finally
            {
                foreach (var com in new[] { items, folder, shell }) if (com != null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com);
            }
            foreach (var a in new[] {
                new AppEntry { Name = "File Explorer", Path = "explorer.exe", Target = @"C:\Windows\explorer.exe" },
                new AppEntry { Name = "Windows Settings", Path = "ms-settings:", Target = "ms-settings:" },
                new AppEntry { Name = "Task Manager", Path = "taskmgr.exe", Target = @"C:\Windows\System32\Taskmgr.exe" }
            }) if (seen.Add(a.Name)) apps.Add(a);
            apps.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { Apps = apps; Ready = true; Changed?.Invoke(); Log.Write("Indexed " + Apps.Count + " apps."); }));
        }) { IsBackground = true, Name = "Harbor app index" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    internal static string Resolve(string path)
    {
        IShellLink? link = null;
        try
        {
            link = (IShellLink)new ShellLink();
            ((IPersistFile)link).Load(path, 0);
            var b = new StringBuilder(2048);
            link.GetPath(b, b.Capacity, IntPtr.Zero, 4);
            string appId = AppIdentity.LinkId(link); if (AppIdentity.Packaged(appId)) return appId;
            return b.Length == 0 ? path : Environment.ExpandEnvironmentVariables(b.ToString());
        }
        catch { return path; }
        finally { if (link != null) Marshal.FinalReleaseComObject(link); }
    }
    internal static (string Path, int Index) ShortcutIcon(string path)
    {
        IShellLink? link = null;
        try
        {
            link = (IShellLink)new ShellLink(); ((IPersistFile)link).Load(path, 0);
            var b = new StringBuilder(2048); link.GetIconLocation(b, b.Capacity, out int index);
            if (b.Length == 0) { link.GetPath(b, b.Capacity, IntPtr.Zero, 4); index = 0; }
            return (Environment.ExpandEnvironmentVariables(b.ToString()), index);
        }
        catch { return ("", 0); }
        finally { if (link != null) Marshal.FinalReleaseComObject(link); }
    }

    internal void SeedPins(Settings settings)
    {
        if (settings.PinsInitialized) return;
        var pins = new List<Pin> { new() { Name = "File Explorer", Path = "explorer.exe", Target = @"C:\Windows\explorer.exe" } };
        string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.lnk"))
                pins.Add(new Pin { Name = System.IO.Path.GetFileNameWithoutExtension(file), Path = file, Target = Resolve(file) });
        }
        catch { }
        // Preserve the user's existing taskbar choices; cap only the initial import.
        settings.Pins = pins.GroupBy(p => p.Target, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(14).ToList();
        settings.PinsInitialized = true;
        settings.Save();
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, IntPtr data, uint flags);
        void GetIDList(out IntPtr p); void SetIDList(IntPtr p);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetHotkey(out short hotkey); void SetHotkey(short hotkey); void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index); void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}

internal static class Icons
{
    static readonly ConcurrentDictionary<string, Task<ImageSource?>> Cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly BlockingCollection<(string Path, TaskCompletionSource<ImageSource?> Done)> Requests = new();
    static Icons()
    {
        var thread = new Thread(() =>
        {
            int com = Native.CoInitializeEx(IntPtr.Zero, 2);
            try
            {
                foreach (var request in Requests.GetConsumingEnumerable())
                {
                    try { request.Done.TrySetResult(Native.FileIcon(request.Path)); }
                    catch { request.Done.TrySetResult(null); }
                }
            }
            finally { if (com >= 0) Native.CoUninitialize(); }
        }) { IsBackground = true, Name = "Harbor icon loader" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    internal static ImageSource? Peek(string path) => Cache.TryGetValue(path, out var task) && task.IsCompletedSuccessfully ? task.Result : null;
    internal static Task<ImageSource?> Get(string path) => Cache.GetOrAdd(path, key =>
    {
        var done = new TaskCompletionSource<ImageSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Requests.Add((key, done));
        return done.Task;
    });
}

internal sealed record WindowEntry(IntPtr Handle, string Title, string Path, string ProcessName, IntPtr Monitor, string AppId = "")
{
    internal string Key => AppId.Length > 0 ? AppId : string.IsNullOrWhiteSpace(Path) ? ProcessName : Path;
    internal string LaunchPath => AppId.Length > 0 ? @"shell:AppsFolder\" + AppId : Path;
    internal bool Matches(Pin pin) => Shell.KeysMatch(Key, pin.Target) || Shell.KeysMatch(Path, pin.Target);
}

internal sealed class WindowTracker
{
    internal void LoadPreview(IEnumerable<WindowEntry> entries)
    {
        if (Settings.TestDataDir == null) throw new InvalidOperationException("Preview windows require an isolated profile.");
        Windows = entries.ToList(); Changed?.Invoke();
    }
    internal List<WindowEntry> Windows { get; private set; } = new();
    internal event Action? Changed;
    readonly Dictionary<uint, (string Path, string Name)> processes = new();
    readonly Dictionary<IntPtr, (uint Pid, string Id, string Path, string Title)> identities = new();
    int busy;
    internal async void Refresh()
    {
        if (Interlocked.Exchange(ref busy, 1) != 0) return;
        try
        {
            var result = await Task.Run(() =>
            {
                int com = Native.CoInitializeEx(IntPtr.Zero, 0);
                try
                {
                var list = new List<WindowEntry>();
                var seenPids = new HashSet<uint>();
                Native.EnumWindows((h, _) =>
                {
                    if (!Native.IsWindowVisible(h)) return true;
                    Native.GetWindowThreadProcessId(h, out uint pid);
                    bool ownFiles = pid == Environment.ProcessId && Native.Title(h).Equals("Harbor — Files", StringComparison.OrdinalIgnoreCase);
                    if (pid == Environment.ProcessId && !ownFiles) return true;
                    long style = Native.GetWindowLong(h, -20).ToInt64();
                    if ((style & 0x80) != 0 || ((style & 0x40000) == 0 && Native.GetWindow(h, 4) != IntPtr.Zero)) return true;
                    string cls = Native.Class(h);
                    if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow") return true;
                    Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4);
                    if (cloaked != 0) return true;
                    string title = Native.Title(h);
                    if (string.IsNullOrWhiteSpace(title)) return true;
                    if ((style & 0x08000000) != 0 && (style & 0x40000) == 0) return true;
                    if (title == "Medal Overlay" && Native.ProcessPath(pid).Contains(@"\Medal\", StringComparison.OrdinalIgnoreCase)) return true;
                    seenPids.Add(pid);
                    if (!processes.TryGetValue(pid, out var proc))
                    {
                        try
                        {
                            using var p = Process.GetProcessById((int)pid);
                            string path = Native.ProcessPath(pid);
                            proc = (path, p.ProcessName);
                            processes[pid] = proc;
                        }
                        catch { return true; }
                    }
                    string identity = "", imagePath = proc.Path;
                    if (!ownFiles)
                    {
                        if (!identities.TryGetValue(h, out var cached) || cached.Pid != pid || (cls == "ApplicationFrameWindow" && (cached.Id.Length == 0 || cached.Title != title)))
                        {
                            var resolved = AppIdentity.ForWindow(h, pid, proc.Path); cached = (pid, resolved.Id, resolved.Path, title); identities[h] = cached;
                        }
                        identity = cached.Id; imagePath = cached.Path;
                    }
                    list.Add(new WindowEntry(h, title, ownFiles ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") : imagePath, ownFiles ? "Harbor Files" : proc.Name, Native.MonitorFromWindow(h, 2), identity));
                    return true;
                }, IntPtr.Zero);
                foreach (uint pid in processes.Keys.Where(p => !seenPids.Contains(p)).ToArray()) processes.Remove(pid);
                foreach (var handle in identities.Keys.Where(h => !list.Any(w => w.Handle == h)).ToArray()) identities.Remove(handle);
                return list;
                }
                finally { if (com >= 0) Native.CoUninitialize(); }
            });
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!result.SequenceEqual(Windows)) { Windows = result; Changed?.Invoke(); }
            });
        }
        catch (Exception ex) { Log.Write("Window list: " + ex.Message); }
        finally { Volatile.Write(ref busy, 0); }
    }
}
