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
    [STAThread]
    static void Main(string[] args)
    {
        // A shell started by an Electron-based development host must launch desktop
        // apps normally instead of inheriting that host's Node-only switch.
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);
        if (args.Length >= 2 && args[0] == "--verify") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; Verification.Run(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--verify-dock-drag") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; DockPlacementVerification.Interactive(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--render-demos") { RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; ModDemoRecorder.Record(args[1]); return; }
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
            Send("exit");
            Thread.Sleep(1200);
            Recovery.ForceRestore();
            return;
        }
        var command = args.FirstOrDefault(a => a is "--launcher" or "--settings" or "--quit" or "--status" or "--quick" or "--calendar" or "--arrange" or "--mods" or "--files" or "--notifications" or "--desktop" or "--appearance" or "--profiles" or "--dock-controls" or "--top-dock-controls");
        if (command != null)
        {
            string result = Send(command == "--quit" ? "exit" : command[2..]);
            if (output != null) File.WriteAllText(output, result);
            if (result != "not running" || command is "--quit" or "--status") return;
        }
        using var mutex = new Mutex(true, @"Local\Harbor.Desktop." + Environment.UserName, out bool first);
        if (!first) { Send(command == "--settings" ? "settings" : "launcher"); return; }
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.DispatcherUnhandledException += (_, e) =>
        {
            Log.Write("Unhandled UI exception: " + e.Exception);
            try { shell?.Dispose(); } catch { Recovery.ForceRestore(); }
            e.Handled = true;
            application.Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { Log.Write("Fatal: " + e.ExceptionObject); Recovery.ForceRestore(); };
        application.Exit += (_, _) => shell?.Dispose();
        try
        {
            Theme.Install();
            shell = new Shell(args.Contains("--bars-only"));
            _ = Listen(application);
            if (command != null) shell.Command(command[2..]);
            application.Run();
            Log.Write("UI loop ended.");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log.Write("Startup failed: " + ex);
            try { shell?.Dispose(); } catch { }
            Recovery.ForceRestore();
            MessageBox.Show("Harbor couldn't start. The Windows taskbar has been restored.\n\n" + ex.Message, "Harbor");
        }
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
