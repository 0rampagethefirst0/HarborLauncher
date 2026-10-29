using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Harbor;

internal static class Verification
{
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory); Settings.TestDataDir = Path.Combine(Path.GetFullPath(directory), "profile");
        var results = new List<object>();
        void Check(string name, bool passed, object? detail = null) { results.Add(new { Name = name, Passed = passed, Detail = detail }); if (!passed) throw new InvalidOperationException(name); }
        bool Rejected(Action action) { try { action(); return false; } catch (Exception ex) when (ex is InvalidDataException or IOException) { return true; } }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Theme.Install();
            string atomicPath = Path.Combine(directory, "atomic-state.json"); AtomicFile.WriteText(atomicPath, "old");
            using (var reader = new FileStream(atomicPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                AtomicFile.WriteText(atomicPath, "new"); Check("Heartbeat-style readers allow atomic replacement", AtomicFile.ReadText(atomicPath) == "new");
            }
            using (var blocker = new FileStream(atomicPath, FileMode.Open, FileAccess.Read, FileShare.Read)) Check("Temporary file locks defer a heartbeat write without throwing", !AtomicFile.TryWriteText(atomicPath, "blocked") && AtomicFile.ReadText(atomicPath) == "new");
            Check("Atomic writes resume after a file lock is released", AtomicFile.TryWriteText(atomicPath, "resumed") && AtomicFile.ReadText(atomicPath) == "resumed");
            var config = Settings.Load(); var mods = new Mods(config);
            int bundledCount = mods.Catalog.Count;
            Check("Bundled mods have unique valid IDs", bundledCount == 36 && mods.Catalog.Select(m => m.Id).Distinct().Count() == bundledCount);
            foreach (var mod in mods.Catalog) { Mods.Validate(mod); mods.Install(mod, false); }
            Check("Every bundled package installs", mods.Catalog.All(mods.Installed));
            var note = mods.Catalog.First(m => m.Component == "notes"); mods.Set(note, "content", "Saved note", false);
            Check("Mod settings persist", Settings.Load().ModOptions[note.Id]["content"] == "Saved note");
            var theme = mods.Catalog.First(m => m.Id == "harbor.forest"); mods.Enable(theme, true); mods.ApplyTheme();
            Check("Theme applies to shared live brushes", Theme.Accent.Color.ToString() == "#FF84D4AC" && !Theme.Accent.IsFrozen);
            mods.Set(theme, "accent", "#FFAA00", false); mods.ApplyTheme(); Check("Theme color can change after resources are installed", Theme.Accent.Color.ToString() == "#FFFFAA00");
            mods.Enable(theme, false); mods.ApplyTheme(); Check("Disabling a theme restores the base appearance", Theme.Accent.Color.ToString() == "#FF8AB4FF");
            string package = Path.Combine(directory, "notes.harbormod"); Mods.Export(note, package);
            mods.Uninstall(note); Check("Uninstall removes the installed manifest", !mods.Installed(note)); mods.Import(package); Check("Exported package imports", mods.Installed(note));
            mods.Set(note, "width", "350", false); string configured = Path.Combine(directory, "configured.harbormod"); mods.ExportConfigured(note, configured); using (var exported = ZipFile.OpenRead(configured)) { using var reader = new StreamReader(exported.GetEntry("manifest.json")!.Open()); var definition = Mods.Read(reader.ReadToEnd()); Check("Package export preserves declared custom settings", definition.Options.First(o => o.Key == "width").Default == "350"); }
            var custom = mods.Create(note, "My Note", "Local"); Check("Mod creator produces an independent installable package", custom.Id != note.Id && mods.Installed(custom) && custom.Options.First(o => o.Key == "width").Default == "350");
            string traversal = Path.Combine(directory, "invalid.harbormod"); using (var archive = ZipFile.Open(traversal, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry("../manifest.json").Open()); writer.Write("{}"); }
            Check("Path traversal package is rejected", Rejected(() => mods.Import(traversal)));
            Check("Unknown components are rejected", Rejected(() => Mods.Read("{\"id\":\"test.invalid\",\"name\":\"Invalid\",\"component\":\"unknown\"}")));
            Check("Invalid mod IDs are rejected", Rejected(() => Mods.Read("{\"id\":\"../invalid\",\"name\":\"Invalid\",\"component\":\"notes\"}")));
            var large = Enumerable.Range(0, 1200).Select(i => new ModDefinition { Id = "fixture.catalog." + i, Name = "Catalog test item " + i, Kind = "theme" }).ToList();
            string catalogPath = Path.Combine(directory, "catalog-scale-fixture.json"); File.WriteAllText(catalogPath, JsonSerializer.Serialize(large));
            var watch = Stopwatch.StartNew(); mods.Import(catalogPath); watch.Stop(); Check("Catalog loads more than 1000 packages", mods.Catalog.Count == bundledCount + 1201, new { Count = mods.Catalog.Count, Milliseconds = watch.ElapsedMilliseconds, TestDataOnly = true });
            var model = new Shell(config); foreach (var mod in model.Mods.Catalog.Where(m => m.Kind == "widget" && m.Author == "Harbor")) { var widget = new Widget(model, mod); widget.Tick(); }
            Check("All native widget components construct and update", true);
            var dashboard = mods.Catalog.First(m => m.Component == "dashboard"); string dashboardPath = Path.Combine(directory, "dashboard.harbormod"); Mods.Export(dashboard, dashboardPath); mods.Import(dashboardPath);
            Check("Custom layout packages survive export and import", mods.Catalog.First(m => m.Id == dashboard.Id).Layout?.Children.Count == dashboard.Layout?.Children.Count);
            var invalidLayout = new WidgetNode { Children = new() { new WidgetNode { Type = "input", Key = "same" }, new WidgetNode { Type = "check", Key = "same" } } };
            Check("Duplicate interactive layout keys are rejected", Rejected(() => WidgetLayout.Validate(invalidLayout)));
            Check("Unknown layout actions are rejected", Rejected(() => WidgetLayout.Validate(new WidgetNode { Type = "button", Action = "unsupported-code" })));
            var deep = new WidgetNode(); var cursor = deep; for (int depth = 0; depth < 12; depth++) { var next = new WidgetNode(); cursor.Children.Add(next); cursor = next; }
            Check("Excessively nested layouts are rejected", Rejected(() => WidgetLayout.Validate(deep)));
            var renderer = new WidgetLayout(model, dashboard, false); var inputNode = new WidgetNode { Type = "input", Key = "sample", Text = "" }; var input = (TextBox)renderer.Build(inputNode, 280); input.Text = "Saved field";
            Check("Custom widget text fields persist", Settings.Load().ModOptions[dashboard.Id]["field.sample"] == "Saved field");
            var previewRenderer = new WidgetLayout(model, dashboard, true); var previewInput = (TextBox)previewRenderer.Build(inputNode, 280); previewInput.Text = "Preview edit";
            Check("Builder previews do not overwrite live widget data", Settings.Load().ModOptions[dashboard.Id]["field.sample"] == "Saved field");
            Check("Live text resolves settings and fields", renderer.Expand("{field.sample} · {option.width}").Contains("Saved field") && !renderer.Expand("{option.width}").Contains('{'));
            config.IconSize = 44; config.DockHeight = 64; config.TopBarHeight = 1000; config.Normalize();
            Check("Appearance sizes keep large icons within the dock", config.DockHeight >= config.IconSize + 32 && config.TopBarHeight == 64);
            config.IconSize = 32; config.DockHeight = 80; config.TopBarHeight = 38; config.Pins = new() { new Pin { Name = "Pinned", Path = "notepad.exe", Target = @"C:\Windows\notepad.exe" } };
            IntPtr monitor = model.CurrentMonitor(); var firstWindow = new WindowEntry(new IntPtr(101), "First", @"C:\one.exe", "One", monitor); var secondWindow = new WindowEntry(new IntPtr(102), "Second", @"C:\two.exe", "Two", monitor);
            model.Tracker.LoadPreview(new[] { firstWindow }); var dock = new Dock(model, monitor); dock.Refresh();
            StackPanel DockItems() => (StackPanel)((ScrollViewer)((Grid)dock.Content).Children.OfType<Border>().First().Child).Content;
            var firstButton = DockItems().Children.OfType<Button>().First(b => AutomationProperties.GetAutomationId(b) == "run:" + firstWindow.Key);
            model.Tracker.LoadPreview(new[] { secondWindow, firstWindow with { Title = "Changed title" } }); dock.Refresh();
            var kept = DockItems().Children.OfType<Button>().First(b => AutomationProperties.GetAutomationId(b) == "run:" + firstWindow.Key);
            Check("Dock preserves existing buttons through window-list and title changes", ReferenceEquals(firstButton, kept));
            model.Tracker.LoadPreview(new[] { firstWindow }); dock.Refresh(); Check("Closing another app keeps the existing dock button", DockItems().Children.OfType<Button>().Contains(firstButton)); dock.Close();
            using (var one = new HwndSource(new HwndSourceParameters("Harbor identity test 1") { ParentWindow = new IntPtr(-3) }))
            using (var two = new HwndSource(new HwndSourceParameters("Harbor identity test 2") { ParentWindow = new IntPtr(-3) }))
            {
                string idOne = "Harbor.Test_abcdefghijklm!One", idTwo = "Harbor.Test_abcdefghijklm!Two";
                AppIdentity.SetOwnWindowId(one.Handle, idOne); AppIdentity.SetOwnWindowId(two.Handle, idTwo);
                Check("Windows window-property identity reads distinct application IDs", AppIdentity.WindowId(one.Handle) == idOne && AppIdentity.WindowId(two.Handle) == idTwo);
                var a = new WindowEntry(one.Handle, "One", "ApplicationFrameHost.exe", "Host", monitor, idOne); var b = new WindowEntry(two.Handle, "Two", "ApplicationFrameHost.exe", "Host", monitor, idTwo);
                Check("Hosted applications get separate dock groups", a.Key != b.Key);
                Check("Packaged app pins match shell launch paths", a.Matches(new Pin { Target = @"shell:AppsFolder\" + idOne }));
                AppIdentity.SetOwnWindowId(one.Handle, ""); AppIdentity.SetOwnWindowId(two.Handle, "");
            }
            string source = Path.Combine(directory, "file-source"), destination = Path.Combine(directory, "file-copy"); Directory.CreateDirectory(Path.Combine(source, "nested")); File.WriteAllText(Path.Combine(source, "nested", "test.txt"), "Keep this content"); FileTools.Copy(source, destination);
            Check("File copy preserves nested content and source", File.ReadAllText(Path.Combine(destination, "nested", "test.txt")) == "Keep this content" && File.Exists(Path.Combine(source, "nested", "test.txt")));
            Check("Copy prevents overwriting an existing file", Rejected(() => FileTools.Copy(Path.Combine(source, "nested", "test.txt"), Path.Combine(destination, "nested", "test.txt"))));
            Check("Copy rejects copying a folder into itself", Rejected(() => FileTools.Copy(source, Path.Combine(source, "inside"))));
            var games = new Games(config);
            Check("Game detection recognizes Rocket League and installed Steam games", games.IsGame(@"C:\Program Files\Epic Games\rocketleague\Binaries\Win64\RocketLeague.exe") && games.IsGame(@"D:\SteamLibrary\steamapps\common\Example\game.exe"));
            Check("Game launchers and normal apps remain visible", !games.IsGame(@"C:\Program Files\Epic Games\Launcher\EpicGamesLauncher.exe") && !games.IsGame(@"C:\Windows\notepad.exe"));
            config.GameExclusions.Add(@"C:\Games\RocketLeague.exe"); Check("Per-app game exclusion overrides automatic detection", !games.IsGame(@"C:\Games\RocketLeague.exe"));
            var notices = new Notifications(config); notices.Add("Verification", "Local test notification"); Check("Harbor notifications persist", new Notifications(config).Items.Any(n => n.Title == "Verification")); notices.DismissAll(); Check("Dismissal clears Harbor notification view", notices.Items.Count == 0);
            Check("Desktop app launch environment is clean", Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE") == null);
            TweakVerification.Run(directory, model, Check);
            CatalogVerification.Run(model, Check);
            PreviewVerification.Run(model, Check);
            app.Shutdown();
        }
        catch (Exception ex) { results.Add(new { Name = "Verification finished", Passed = false, Detail = ex.ToString() }); }
        File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(results, Mods.Json));
    }
}
