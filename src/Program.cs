using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Harbor;

internal static class Program
{
    internal static string Version => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.6.0";
    static string PipeName => "Harbor.Desktop." + Environment.UserName;
    static Shell? shell;
    static StartupPresentation? startup;
    [STAThread]
    static void Main(string[] args)
    {
        // A shell started by an Electron-based development host must launch desktop
        // apps normally instead of inheriting that host's Node-only switch.
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);
        ShellSession.IsShellProcess = args.Contains("--shell-session");
        if (args.Contains("--install-shell")) { ShellSession.Install(); return; }
        if (args.Contains("--preview-intro") || args.Contains("--probe-intro")) {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; if (args.Contains("--with-shell-theme")) Theme.Install(); var watch = System.Diagnostics.Stopwatch.StartNew();
            using var preview = new StartupPresentation(Settings.Load(), true, args.Contains("--probe-intro")); bool finished = preview.Wait(12000); int destination = Array.IndexOf(args, "--output");
            if (destination >= 0 && args.Length > destination + 1) File.WriteAllText(args[destination + 1], System.Text.Json.JsonSerializer.Serialize(new { Finished = finished, preview.WindowsCreated, preview.Error, preview.RenderedFrames, preview.RenderedFPS, preview.TargetFPS, preview.FramePacing, ElapsedMilliseconds = watch.ElapsedMilliseconds, RenderMode = RenderOptions.ProcessRenderMode.ToString() }));
            Environment.ExitCode = finished && preview.Error == null ? 0 : 1; return;
        }
        if (args.Length >= 2 && args[0] == "--verify") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; Verification.Run(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--verify-home-motion") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; HomeMotionVerification.Run(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--verify-fades") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; WindowFadeVerification.Run(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--verify-scroll") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; ScrollVerification.Run(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--verify-dock-drag") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; DockPlacementVerification.Interactive(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--render-demos") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; ModDemoRecorder.Record(args[1], args.Contains("--widgets-only")); return; }
        if (args.Length >= 3 && args[0] == "--render-demo") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; ModDemoRecorder.Record(args[2], onlyId: args[1]); return; }
        if (args.Length >= 2 && args[0] == "--export-catalog") { ModDemoRecorder.ExportCatalog(args[1]); return; }
        if (args.Length >= 3 && args[0] == "--optimize-demos") { ModDemoRecorder.Optimize(args[1], args[2]); return; }
        if (args.Length >= 2 && args[0] == "--probe-shell") { Diagnostics.Probe(args[1]); return; }
        if (args.Length >= 3 && args[0] == "--watchdog") { Recovery.Watch(int.Parse(args[1]), long.Parse(args[2])); return; }
        if (args.Length >= 2 && args[0] == "--diagnose") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; Diagnostics.Run(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--render-check") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; Diagnostics.Visual(args[1]); return; }
        string? output = null;
        int outputIndex = Array.IndexOf(args, "--output");
        if (outputIndex >= 0 && args.Length > outputIndex + 1) output = args[outputIndex + 1];
        if (args.Contains("--restore"))
        {
            ShellSession.RestoreRegistration();
            Send("exit");
            Thread.Sleep(1200);
            Recovery.ForceRestore();
            ShellSession.EnsureExplorer();
            return;
        }
        int folderIndex = Array.IndexOf(args, "--open-folder");
        string? folder = folderIndex >= 0 && args.Length > folderIndex + 1 ? NormalizeFolderArgument(args[folderIndex + 1]) : null;
        int pluginIndex = Array.IndexOf(args, "--open-plugin");
        string? plugin = pluginIndex >= 0 && args.Length > pluginIndex + 1 ? args[pluginIndex + 1] : null;
        if (plugin != null && Send("plugin:" + plugin) != "not running") return;
        if (folder != null && Send("files-path:" + folder) != "not running") return;
        var command = args.FirstOrDefault(a => a is "--screenshot-all" or "--screenshot-window" or "--cancel-screenshot" or "--screenshot" or "--close-panels" or "--home" or "--home-settings" or "--intro" or "--launcher" or "--settings" or "--quit" or "--status" or "--quick" or "--calendar" or "--arrange" or "--mods" or "--files" or "--notifications" or "--desktop" or "--appearance" or "--profiles" or "--dock-controls" or "--top-dock-controls");
        if (command != null)
        {
            string result = Send(command == "--quit" ? "exit" : command[2..]);
            if (output != null) File.WriteAllText(output, result);
            if (result != "not running" || command is "--quit" or "--status") return;
        }
        if (!ShellSession.IsShellProcess && ShellSession.Enabled)
        {
            Process.Start(new ProcessStartInfo(ShellSession.HostPath) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })?.Dispose();
            if (command != null || folder != null || plugin != null)
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Thread.Sleep(500);
                    if (Send(plugin != null ? "plugin:" + plugin : folder != null ? "files-path:" + folder : command![2..]) == "ok") break;
                }
            return;
        }
        using var mutex = new Mutex(true, @"Local\Harbor.Desktop." + Environment.UserName, out bool first);
        if (!first) { Send(command == "--settings" ? "settings" : "launcher"); return; }
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.DispatcherUnhandledException += (_, e) =>
        {
            Environment.ExitCode = 1;
            Log.Write("Unhandled UI exception: " + e.Exception);
            try { shell?.Dispose(); } catch { Recovery.ForceRestore(); }
            if (ShellSession.IsShellProcess && !ShellSession.HostRunning) ShellSession.EnsureExplorer();
            e.Handled = true;
            application.Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { Log.Write("Fatal: " + e.ExceptionObject); Recovery.ForceRestore(); };
        application.Exit += (_, _) => { startup?.Dispose(); shell?.Dispose(); };
        try
        {
            Theme.Install();
            var introSettings = Settings.Load(); new Mods(introSettings).ApplyTheme(); var focusedGame = new Games(introSettings); focusedGame.Update();
            if (!args.Contains("--skip-intro") && !focusedGame.Active && StartupPresentation.ShouldShow(introSettings)) startup = new StartupPresentation(introSettings);
            shell = new Shell(args.Contains("--bars-only"), message => startup?.Stage(message));
            _ = Listen(application);
            startup?.Ready();
            if (command != null) shell.Command(command[2..]);
            if (folder != null) shell.Command("files-path:" + folder);
            if (plugin != null) shell.Command("plugin:" + plugin);
            application.Run();
            Log.Write("UI loop ended.");
            Environment.Exit(Environment.ExitCode);
        }
        catch (Exception ex)
        {
            startup?.Dispose();
            Log.Write("Startup failed: " + ex);
            try { shell?.Dispose(); } catch { }
            Recovery.ForceRestore();
            Environment.ExitCode = 1;
            if (ShellSession.IsShellProcess && !ShellSession.HostRunning) ShellSession.EnsureExplorer();
            if (!ShellSession.IsShellProcess) MessageBox.Show("Harbor couldn't start. The Windows taskbar has been restored.\n\n" + ex.Message, "Harbor");
        }
    }
    internal static string NormalizeFolderArgument(string path)
    {
        // ShellExecute can supply a quoted drive root ending in a backslash;
        // Windows command-line parsing consumes that slash before the quote.
        path = path.TrimEnd('"');
        return path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':' ? path + "\\" : path;
    }
    static string Send(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            client.Connect(2000);
            var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client);
            writer.WriteLine(command);
            var read = reader.ReadToEndAsync();
            return read.Wait(3000) ? read.Result : "not responding";
        }
        catch { return "not running"; }
    }
    static async Task Listen(Application app)
    {
        while (!app.Dispatcher.HasShutdownStarted)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync().ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                string? command = await reader.ReadLineAsync().ConfigureAwait(false);
                string result = await app.Dispatcher.InvokeAsync(() => shell?.Command(command ?? "status") ?? "not running");
                await writer.WriteAsync(result).ConfigureAwait(false);
            }
            catch (Exception ex) { if (!app.Dispatcher.HasShutdownStarted) { Log.Write("Local command: " + ex.Message); await Task.Delay(500); } }
        }
    }
}
