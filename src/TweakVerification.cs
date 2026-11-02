using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Harbor;

internal static class TweakVerification
{
    internal static void Run(string directory, Shell shell, Action<string, bool, object?> check)
    {
        void C(string name, bool passed, object? detail = null) => check(name, passed, detail);
        bool Rejected(Action action) { try { action(); return false; } catch (Exception ex) when (ex is InvalidDataException or IOException) { return true; } }
        var mods = shell.Mods; var config = shell.Config;
        C("Twelve distinct behavior packages expose supported capabilities", mods.Catalog.Count(m => m.Kind == "tweak" && m.Author == "Harbor") == 12 && mods.Catalog.Where(m => m.Kind == "tweak").All(m => Tweaks.Components.Contains(m.Component)));
        var app = new WindowEntry(new IntPtr(11), "Project notes — Editor", @"C:\Apps\Editor.exe", "Editor", IntPtr.Zero);
        C("App rules match case-insensitive executable globs", Tweaks.Matches("edit*.exe", "*notes*", "", app) && !Tweaks.Matches("", "", "", app));
        C("App and title exclusions override broad matches", !Tweaks.Matches("*", "", "*notes*", app) && !Tweaks.Matches("*", "*browser*", "", app));
        var packaged = app with { AppId = "Vendor.Editor_abcdefghijklm!App", ProcessName = "ApplicationFrameHost" };
        C("Rules target packaged identities independently of the host", Tweaks.Matches("Vendor.Editor_*!App", "", "", packaged) && !Tweaks.Matches("Vendor.Other*!App", "", "", packaged));
        C("Glob matching handles wildcard retry and literal paths", Tweaks.Wildcard(@"C:\*\Editor.???", app.Path) && !Tweaks.Wildcard("*x*y*z", new string('x', 500)));
        var work = new Native.Rect { Left = 1920, Top = -621, Right = 3360, Bottom = 1821 };
        var left = Tweaks.Region(work, "left", 12); var right = Tweaks.Region(work, "right", 12); var corner = Tweaks.Region(work, "bottom-right", 12);
        C("Placement regions respect negative-origin monitor work areas", left.Left == work.Left + 12 && right.Right <= work.Right - 12 && left.Right + 12 <= right.Left && corner.Top >= work.Top + work.Height / 2 && corner.Bottom <= work.Bottom - 12);
        var oldMax = new Native.Rect { Left = -8, Top = 40, Right = 1928, Bottom = 1088 }; var oldFrame = new Native.Rect { Left = 0, Top = 48, Right = 1920, Bottom = 1080 }; var primaryWork = new Native.Rect { Left = 0, Top = 38, Right = 1920, Bottom = 1000 };
        var fitted = WindowSpace.Fit(oldMax, oldFrame, primaryWork, true);
        C("Maximized fitting reserves both bars while preserving invisible frame borders", fitted.Left == -8 && fitted.Top == 30 && fitted.Right == 1928 && fitted.Bottom == 1008);
        var normal = WindowSpace.Fit(new Native.Rect { Left = 400, Top = 460, Right = 1400, Bottom = 1080 }, new Native.Rect { Left = 400, Top = 460, Right = 1400, Bottom = 1080 }, primaryWork, false);
        C("Ordinary window fitting keeps its size while uncovering bottom controls", normal.Width == 1000 && normal.Height == 620 && normal.Top == 380 && normal.Bottom == 1000);
        var lease = new DockLease();
        C("Hovering alone never unlocks the dock", !lease.Update(1000, true, false, 3000));
        lease.Grant(1000, 3000); C("A granted dock stays usable while the pointer remains inside", lease.Update(20000, true, false, 3000));
        C("Dock context menus retain the interaction session", lease.Update(40000, false, true, 3000));
        C("Dock usability continues briefly after leaving", lease.Update(42999, false, false, 3000));
        C("Dock usability expires after the configured delay", !lease.Update(43001, false, false, 3000));
        C("The first right-click grants activation without requesting a context menu", lease.BeginRightClick(44000, 3000));
        C("A second right-click in the session allows the normal context menu", !lease.BeginRightClick(45000, 3000));
        var typing = new DockTyping(); IntPtr typingMonitor = new IntPtr(1), otherMonitor = new IntPtr(2); typing.Mark(typingMonitor, 1000);
        C("Typing keeps only its monitor's dock in quiet mode", typing.Active(typingMonitor, 2000) && !typing.Active(otherMonitor, 2000));
        C("Typing quiet mode survives repeated activity", typing.Active(typingMonitor, 2400)); typing.Mark(typingMonitor, 2400);
        bool expired = typing.Active(typingMonitor, 3800) && !typing.Active(typingMonitor, 4000); typing.Mark(typingMonitor, 5000); typing.Clear(typingMonitor);
        C("Typing quiet mode expires after a pause and can be explicitly cleared", expired && !typing.Active(typingMonitor, 5001));
        C("Typing detection distinguishes text keys from menu shortcuts", DockTyping.TextKey(0x41) && DockTyping.TextKey(8) && !DockTyping.TextKey(0x1B) && !DockTyping.TextKey(0x77) && !DockTyping.TextKey(0x5B));

        string catalog = Path.Combine(directory, "dependency-catalog.json");
        ModDefinition Def(string id) => new() { Id = id, Name = id, Kind = "theme" };
        var dependency = Def("test.dependency"); var parent = Def("test.parent"); parent.Dependencies.Add(new() { Id = dependency.Id, MinimumVersion = "1.0.0" });
        Import(dependency, parent); mods.Enable(parent, true);
        C("Enabling a module installs and enables dependencies first", mods.Installed(dependency) && config.EnabledMods.IndexOf(dependency.Id) < config.EnabledMods.IndexOf(parent.Id));
        mods.Enable(dependency, false); C("Disabling a dependency disables dependent modules", !mods.EnabledMod(parent) && !mods.EnabledMod(dependency));
        var missing = Def("test.missing"); missing.Dependencies.Add(new() { Id = "missing.package" }); var before = config.EnabledMods.ToArray();
        C("Missing dependencies reject enable without changing active mods", Rejected(() => mods.Enable(missing, true)) && config.EnabledMods.SequenceEqual(before));
        var future = Def("test.future"); future.MinimumHarborVersion = "99.0.0";
        C("Incompatible Harbor versions cannot be enabled", Rejected(() => mods.Enable(future, true)));
        var one = Def("test.cycle-one"); var two = Def("test.cycle-two"); one.Dependencies.Add(new() { Id = two.Id }); two.Dependencies.Add(new() { Id = one.Id }); Import(one, two);
        C("Dependency cycles are reported before activation", mods.PlanEnable(one).Errors.Any(e => e.Contains("cycle")) && Rejected(() => mods.Enable(one, true)));
        var conflict = Def("test.conflict"); conflict.Conflicts.Add(dependency.Id); Import(conflict, Def("test.companion")); mods.Enable(dependency, true);
        C("Conflicting modules require explicit replacement", Rejected(() => mods.Enable(conflict, true)) && mods.EnabledMod(dependency)); mods.Enable(conflict, true, true);
        C("Explicit replacement disables the conflicting module", mods.EnabledMod(conflict) && !mods.EnabledMod(dependency)); mods.Enable(conflict, false);
        var label = mods.Catalog.First(m => m.Id == "harbor.dock-labels"); var clone = mods.Create(label, "Label test", "Test"); mods.Enable(label, true);
        C("Duplicate shell behavior modules expose a conflict", mods.PlanEnable(clone).Conflicts.Any(m => m.Id == label.Id)); mods.Enable(label, false);
        var newer = Def(dependency.Id); newer.Version = "2.0.0"; newer.Options.Add(new() { Key = "accent", Label = "Accent", Default = "#ABCDEF", Type = "color" }); Import(newer, Def("test.update-companion"));
        C("Catalog updates are distinct from installed module versions", mods.UpdateAvailable(newer) && mods.InstalledVersion(newer) == "1.0.0"); mods.Enable(newer, true);
        C("Enabling an available update installs the newer definition", mods.InstalledVersion(newer) == "2.0.0");
        C("Malformed dependency metadata is rejected", Rejected(() => Mods.Read("{\"id\":\"test.bad\",\"name\":\"Bad\",\"kind\":\"theme\",\"dependencies\":[null]}")));

        var note = mods.Catalog.First(m => m.Id == "harbor.notes"); mods.Set(note, "content", "Private note stays local", false); mods.Set(note, "x-2", "123", false);
        var profile = Profiles.Capture(config, mods, "Work"); string exported = JsonSerializer.Serialize(profile, Mods.Json);
        C("Profiles capture layout settings without notes or clipboard data", profile.Options[note.Id]["x-2"] == "123" && !exported.Contains("Private note stays local") && !profile.Options[note.Id].ContainsKey("content"));
        var read = Profiles.Read(exported); config.Accent = "#102030"; var savedPins = config.Pins; config.ShowMedia = !config.ShowMedia;
        Profiles.Apply(config, mods, read);
        C("Profiles restore appearance and enabled modules while retaining pins and notes", config.Accent == read.Appearance["Accent"].GetString() && ReferenceEquals(config.Pins, savedPins) && mods.Value(note, "content") == "Private note stays local" && config.EnabledMods.Contains(newer.Id));
        C("Profile switching saves an undo snapshot", config.PreviousProfile?.Appearance["Accent"].GetString() == "#102030");
        var broken = Profiles.Read(exported); broken.EnabledMods.Add("not.available"); string enabledBefore = string.Join("|", config.EnabledMods); string accentBefore = config.Accent;
        C("Missing profile packages leave the desktop configuration unchanged", Rejected(() => Profiles.Apply(config, mods, broken)) && accentBefore == config.Accent && enabledBefore == string.Join("|", config.EnabledMods));
        C("Profiles cannot import startup or shell recovery switches", Rejected(() => Profiles.Read("{\"appearance\":{\"ReplaceTaskbar\":false}}")));
        C("Profile appearance values require the correct property type", Rejected(() => Profiles.Read("{\"appearance\":{\"IconSize\":\"huge\"}}")));
        C("Invalid profile colors are rejected before changing the desktop", Rejected(() => Profiles.Read("{\"appearance\":{\"Accent\":\"not a color\"}}")));
        C("Profile import and export preserve mod option values", read.Options[newer.Id]["accent"] == profile.Options[newer.Id]["accent"]);

        mods.Enable(label, true); mods.Configure(label, new Dictionary<string, string> { ["label"] = "app", ["width"] = "120", ["when"] = "always" });
        bool showRunningApps = config.TaskbarShowRunningApps; config.TaskbarShowRunningApps = true;
        IntPtr monitor = shell.CurrentMonitor(); var first = new WindowEntry(new IntPtr(101), "First app window", @"C:\test-one.exe", "One", monitor); shell.Tracker.LoadPreview(new[] { first });
        var dock = new Dock(shell, monitor); dock.Refresh(); var items = (StackPanel)((ScrollViewer)((Grid)dock.Content).Children.OfType<Border>().First().Child).Content;
        var button = items.Children.OfType<Button>().First(b => AutomationProperties.GetAutomationId(b) == "run:" + first.Key); var body = (Grid)button.Content;
        C("Dock labels render app names beside existing icons", body.Children.OfType<TextBlock>().Any(t => t.Text == "One"));
        shell.Tracker.LoadPreview(new[] { first with { Title = "New title" } }); dock.Refresh(); C("Label updates preserve the dock button identity", items.Children.OfType<Button>().Contains(button));
        var dockHandle = new WindowInteropHelper(dock).EnsureHandle(); dock.SetInputMode(true, .45);
        C("A faded dock has native click-through and no-activate styles", (Native.GetWindowLong(dockHandle, -20).ToInt64() & 0x08000020) == 0x08000020 && dock.InputPassThrough && !dock.IsHitTestVisible && Math.Abs(dock.Opacity - .45) < .001);
        dock.SetInputMode(false, .45);
        C("An interactive dock restores native hit testing and full opacity", (Native.GetWindowLong(dockHandle, -20).ToInt64() & 0x08000020) == 0 && !dock.InputPassThrough && dock.IsHitTestVisible && dock.Opacity == 1);
        dock.Close(); config.TaskbarShowRunningApps = showRunningApps; mods.Enable(label, false);
        var monitorBounds = Native.Monitor(monitor).Monitor; var controlBox = new DockControls(shell, monitor, monitorBounds);
        IEnumerable<DependencyObject> Contents(DependencyObject root) { yield return root; foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var nested in Contents(child)) yield return nested; }
        C("Taskbar style keeps three independent half-step transparency controls", Contents(controlBox).OfType<Slider>().Count(s => s.Minimum == .5 && s.Maximum == 99.5 && s.TickFrequency == .5) == 3);
        controlBox.PreparePreview("Apps"); C("Folder app sizing remains available in the Apps tab", Contents(controlBox).OfType<Slider>().Any(s => AutomationProperties.GetName(s) == "Apps inside folders" && s.Maximum == 96)); controlBox.Close();
        config.DockMotionMilliseconds = 900; config.Normalize(); bool bounded = config.DockMotionMilliseconds == 350; config.DockMotionMilliseconds = 0; config.Normalize(); C("Dock animation duration is bounded and supports turning motion off", bounded && config.DockMotionMilliseconds == 0); config.DockMotionMilliseconds = 170;

        using (var window = new NativeWindow())
        using (var recovery = new Recovery(false))
        {
            var h = window.Handle; long original = Native.GetWindowLong(h, -20).ToInt64();
            var top = recovery.AcquireEffect(h, "window-topmost")!; bool setTop = WindowEffects.SetTopmost(top, true);
            string savedJson = AtomicFile.ReadText(Path.Combine(Settings.DataDir, "recovery.json")); var savedState = JsonSerializer.Deserialize<RecoveryState>(savedJson)!;
            C("Native always-on-top captures restoration data before applying", (Native.GetWindowLong(h, -20).ToInt64() & 8) != 0 && savedState.WindowEffects.Any(s => s.Handle == h.ToInt64() && s.RestoreTopmost), new { SetResult = setTop, CurrentStyle = Native.GetWindowLong(h, -20).ToInt64(), OriginalStyle = original, Parent = Native.GetParent(h).ToInt64(), WindowStyle = Native.GetWindowLong(h, -16).ToInt64(), Matches = WindowEffects.Matches(top), State = savedState.WindowEffects });
            var opacity = recovery.AcquireEffect(h, "window-opacity")!; WindowEffects.SetOpacity(opacity, 50); WindowEffects.GetLayeredWindowAttributes(h, out _, out byte alpha, out _);
            C("Native opacity applies an exact half-opacity alpha", alpha == 128);
            WindowEffects.SetOpacity(opacity, .5); WindowEffects.GetLayeredWindowAttributes(h, out _, out byte minimumAlpha, out _); C("Window opacity accepts a fractional half-percent value", minimumAlpha == 1); WindowEffects.SetOpacity(opacity, 50);
            var recorded = JsonSerializer.Deserialize<RecoveryState>(AtomicFile.ReadText(Path.Combine(Settings.DataDir, "recovery.json")))!;
            C("Recovery state serializes both effects with window identity tokens", recorded.WindowEffects.Count == 1 && recorded.WindowEffects[0].RestoreTopmost && recorded.WindowEffects[0].RestoreOpacity && recorded.WindowEffects[0].Token != 0);
            var wrong = JsonSerializer.Deserialize<WindowEffectState>(JsonSerializer.Serialize(recorded.WindowEffects[0]))!; wrong.Token++; WindowEffects.Restore(wrong);
            C("Stale window tokens cannot restore a different window", !WindowEffects.Matches(wrong) && (Native.GetWindowLong(h, -20).ToInt64() & 8) != 0);
            foreach (var state in recorded.WindowEffects) WindowEffects.Restore(state);
            C("Serialized recovery restores original topmost and layered state", Native.GetWindowLong(h, -20).ToInt64() == original && !WindowEffects.Matches(top));
            recovery.AcquireEffect(h, "window-topmost");
            string statePath = Path.Combine(Settings.DataDir, "recovery.json");
            using (var blocker = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                C("A locked recovery file prevents new effects from changing a window", Rejected(() => recovery.AcquireEffect(h, "window-opacity")) && (Native.GetWindowLong(h, -20).ToInt64() & 0x80000) == 0);
            recovery.ReleaseAllEffects();
            Native.SetWindowLong(h, -20, new IntPtr(original | 0x80000)); WindowEffects.SetLayeredWindowAttributes(h, 0, 210, 2);
            var layered = recovery.AcquireEffect(h, "window-opacity")!; WindowEffects.SetOpacity(layered, 60); recovery.ReleaseEffect(h, "window-opacity"); WindowEffects.GetLayeredWindowAttributes(h, out _, out alpha, out uint flags);
            C("Existing layered opacity is restored without removing its original style", alpha == 210 && flags == 2 && (Native.GetWindowLong(h, -20).ToInt64() & 0x80000) != 0);
            Native.SetWindowLong(h, -20, new IntPtr(original));
        }
        var sessions = Audio.Sessions();
        C("Windows audio-session enumeration returns valid levels", Audio.SessionError.Length == 0 && sessions.All(s => s.Volume >= 0 && s.Volume <= 1 && s.ProcessId != 0), new { Count = sessions.Count, Error = Audio.SessionError });
        using (var silence = new SilentAudio())
        {
            AppAudioSession? own = null;
            for (int i = 0; i < 20 && own == null; i++) { own = Audio.Sessions().FirstOrDefault(s => s.ProcessId == Environment.ProcessId); if (own == null) Thread.Sleep(50); }
            C("An owned silent audio stream appears as its own session", own != null);
            float delta = own!.Volume > .5f ? -.1f : .1f;
            var others = Audio.Sessions().Where(s => s.ProcessId != Environment.ProcessId).ToList();
            try
            {
                int count = Audio.AdjustApp(new[] { (uint)Environment.ProcessId }, Array.Empty<string>(), delta); var changed = Audio.Sessions().First(s => s.ProcessId == Environment.ProcessId);
                C("App-volume adjustment changes only the selected process session", count > 0 && Math.Abs(changed.Volume - Math.Clamp(own.Volume + delta, 0, 1)) < .005f);
                var after = Audio.Sessions(); C("Other process audio levels stay unchanged", others.All(s => after.Any(a => a.ProcessId == s.ProcessId && a.Path == s.Path && Math.Abs(a.Volume - s.Volume) < .005f)), new { OtherSessions = others.Count });
            }
            finally { Audio.AdjustApp(new[] { (uint)Environment.ProcessId }, Array.Empty<string>(), -delta); }
        }
        void Import(params ModDefinition[] definitions) { File.WriteAllText(catalog, JsonSerializer.Serialize(definitions, Mods.Json)); mods.Import(catalog); }
    }
    sealed class NativeWindow : IDisposable
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint extended, string name, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
        // Z-order changes are deferred for hidden windows. Keep this owned tool
        // window visible outside all desktop bounds, without activating it.
        internal IntPtr Handle { get; } = CreateWindowEx(0x08000080, "STATIC", "Harbor native effect check", 0x10CF0000, -30000, -30000, 500, 300, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        public void Dispose() { if (Handle != IntPtr.Zero) DestroyWindow(Handle); }
    }
    sealed class SilentAudio : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)] struct Format { public ushort Tag, Channels; public uint Samples, Bytes; public ushort Align, Bits, Extra; }
        [StructLayout(LayoutKind.Sequential)] struct Header { public IntPtr Data; public uint Length, Recorded; public IntPtr User; public uint Flags, Loops; public IntPtr Next, Reserved; }
        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr handle, uint device, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr handle);
        IntPtr handle, data, header;
        public SilentAudio()
        {
            var format = new Format { Tag = 1, Channels = 1, Samples = 48000, Bytes = 96000, Align = 2, Bits = 16 };
            if (waveOutOpen(out handle, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0) throw new InvalidOperationException("Silent audio check could not open a playback stream.");
            try { byte[] zeros = new byte[192000]; data = Marshal.AllocHGlobal(zeros.Length); Marshal.Copy(zeros, 0, data, zeros.Length); header = Marshal.AllocHGlobal(Marshal.SizeOf<Header>()); Marshal.StructureToPtr(new Header { Data = data, Length = (uint)zeros.Length, Loops = 1 }, header, false); if (waveOutPrepareHeader(handle, header, (uint)Marshal.SizeOf<Header>()) != 0 || waveOutWrite(handle, header, (uint)Marshal.SizeOf<Header>()) != 0) throw new InvalidOperationException("Silent audio check could not queue its stream."); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { if (handle != IntPtr.Zero) { waveOutReset(handle); if (header != IntPtr.Zero) waveOutUnprepareHeader(handle, header, (uint)Marshal.SizeOf<Header>()); waveOutClose(handle); handle = IntPtr.Zero; } if (header != IntPtr.Zero) { Marshal.FreeHGlobal(header); header = IntPtr.Zero; } if (data != IntPtr.Zero) { Marshal.FreeHGlobal(data); data = IntPtr.Zero; } }
    }
}
