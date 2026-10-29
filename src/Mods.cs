using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Harbor;

public sealed class ModOption
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
    public string Default { get; set; } = "";
    public string[] Choices { get; set; } = Array.Empty<string>();
}
public sealed class ModDependency
{
    public string Id { get; set; } = "";
    public string MinimumVersion { get; set; } = "0.1.0";
}
public sealed class ModDefinition
{
    public int Format { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "Harbor";
    public string Version { get; set; } = "1.0.0";
    public string Category { get; set; } = "Tools";
    public string MainCategory { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string PreviewImage { get; set; } = "";
    public string PreviewAccent { get; set; } = "";
    public string Description { get; set; } = "";
    public string Kind { get; set; } = "widget";
    public string Component { get; set; } = "";
    public List<ModOption> Options { get; set; } = new();
    public Dictionary<string, string> Values { get; set; } = new();
    public WidgetNode? Layout { get; set; }
    public string MinimumHarborVersion { get; set; } = "0.1.0";
    public List<string> Tags { get; set; } = new();
    public List<ModDependency> Dependencies { get; set; } = new();
    public List<string> Conflicts { get; set; } = new();
    public string Summary => Kind == "tweak" ? "Behavior mod · " + Category : Kind == "theme" ? "Theme · " + Author : "Widget · " + Author;
    public override string ToString() => Name;
}

internal sealed class Mods
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    readonly Settings settings;
    readonly Dictionary<string, ModDefinition> installed = new(StringComparer.OrdinalIgnoreCase);
    internal List<ModDefinition> Catalog { get; private set; } = new();
    internal event Action? Changed;
    internal static string Store => Path.Combine(Settings.DataDir, "Mods");
    internal static readonly HashSet<string> Components = new(StringComparer.OrdinalIgnoreCase) { "clock", "world-clock", "system", "network", "storage", "uptime", "notes", "tasks", "focus", "stopwatch", "calculator", "converter", "folders", "clipboard", "layouts", "launchpad", "dashboard" };
    internal Mods(Settings settings)
    {
        this.settings = settings;
        Catalog = Bundled();
        Directory.CreateDirectory(Store);
        foreach (string path in Directory.EnumerateFiles(Store, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                var mod = Read(File.ReadAllText(path)); var bundled = Catalog.FirstOrDefault(m => m.Id == mod.Id);
                if (bundled != null && mod.Author == "Harbor" && System.Version.Parse(bundled.Version) > System.Version.Parse(mod.Version)) { mod = bundled; Atomic(path, JsonSerializer.Serialize(mod, Json)); }
                installed[mod.Id] = mod; Upsert(mod);
            }
            catch (Exception ex) { Log.Write("Mod skipped: " + path + ": " + ex.Message); }
        }
        string catalogs = Path.Combine(Settings.DataDir, "Catalogs"); Directory.CreateDirectory(catalogs);
        foreach (string path in Directory.EnumerateFiles(catalogs, "*.json"))
            try { foreach (var mod in JsonSerializer.Deserialize<List<ModDefinition>>(File.ReadAllText(path), Json) ?? new()) { Validate(mod); Upsert(mod); } } catch (Exception ex) { Log.Write("Catalog skipped: " + ex.Message); }
        if (!settings.ModsInitialized)
        {
            foreach (string id in new[] { "harbor.clock", "harbor.system" }) { Install(Catalog.First(m => m.Id == id), false); settings.EnabledMods.Add(id); }
            settings.ModsInitialized = true; settings.Save();
        }
        Sort();
    }
    void Upsert(ModDefinition mod) { int i = Catalog.FindIndex(m => m.Id.Equals(mod.Id, StringComparison.OrdinalIgnoreCase)); if (i < 0) Catalog.Add(mod); else Catalog[i] = mod; }
    void Sort() => Catalog = Catalog.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    internal IEnumerable<ModDefinition> Enabled => settings.EnabledMods.Select(id => installed.GetValueOrDefault(id)).OfType<ModDefinition>();
    internal bool Installed(ModDefinition mod) => installed.ContainsKey(mod.Id);
    internal string InstalledVersion(ModDefinition mod) => installed.TryGetValue(mod.Id, out var current) ? current.Version : "";
    internal bool UpdateAvailable(ModDefinition mod) => installed.TryGetValue(mod.Id, out var current) && System.Version.Parse(mod.Version) > System.Version.Parse(current.Version);
    internal bool EnabledMod(ModDefinition mod) => settings.EnabledMods.Contains(mod.Id, StringComparer.OrdinalIgnoreCase);
    internal string Value(ModDefinition mod, string key, string fallback = "") => settings.ModOptions.TryGetValue(mod.Id, out var options) && options.TryGetValue(key, out var value) ? value : mod.Options.FirstOrDefault(o => o.Key == key)?.Default ?? mod.Values.GetValueOrDefault(key, fallback);
    internal double Number(ModDefinition mod, string key, double fallback, double min, double max) => double.TryParse(Value(mod, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    internal void Set(ModDefinition mod, string key, string value, bool rebuild = true)
    {
        if (!settings.ModOptions.TryGetValue(mod.Id, out var options)) settings.ModOptions[mod.Id] = options = new();
        options[key] = value; settings.Save(); if (rebuild) Changed?.Invoke();
    }
    internal void Configure(ModDefinition mod, IReadOnlyDictionary<string, string> values)
    {
        if (!settings.ModOptions.TryGetValue(mod.Id, out var options)) settings.ModOptions[mod.Id] = options = new();
        foreach (var option in mod.Options) if (values.TryGetValue(option.Key, out var value)) options[option.Key] = value;
        settings.Save();
    }
    internal void Install(ModDefinition mod, bool notify = true)
    {
        Validate(mod); string folder = Path.Combine(Store, mod.Id); Directory.CreateDirectory(folder);
        Atomic(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(mod, Json)); installed[mod.Id] = mod;
        if (notify) Changed?.Invoke();
    }
    internal ModDefinition Create(ModDefinition original, string name, string author)
    {
        var mod = Read(JsonSerializer.Serialize(original, Json));
        string slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-'); if (slug.Length == 0) slug = "mod";
        mod.Id = "local." + slug[..Math.Min(50, slug.Length)] + "." + Guid.NewGuid().ToString("N")[..6]; mod.Name = name; mod.Author = author; mod.Version = "1.0.0";
        foreach (var option in mod.Options) option.Default = Value(original, option.Key);
        Validate(mod); Install(mod, false); Upsert(mod); Sort(); Changed?.Invoke(); return mod;
    }
    internal void UpdateDefinition(ModDefinition mod) { Validate(mod); Install(mod, false); Upsert(mod); Sort(); Changed?.Invoke(); }
    internal void ExportConfigured(ModDefinition original, string path)
    {
        var mod = Read(JsonSerializer.Serialize(original, Json));
        foreach (var option in mod.Options) option.Default = Value(original, option.Key);
        Export(mod, path);
    }
    internal void Enable(ModDefinition mod, bool enabled, bool replaceConflicts = false)
    {
        var previous = settings.EnabledMods.ToList();
        try
        {
        if (enabled)
        {
            var plan = PlanEnable(mod);
            if (plan.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, plan.Errors));
            if (plan.Conflicts.Count > 0 && !replaceConflicts) throw new InvalidDataException("Disable conflicting mods first: " + string.Join(", ", plan.Conflicts.Select(m => m.Name)));
            foreach (var item in plan.Enable) if (!Installed(item) || UpdateAvailable(item)) Install(item, false);
            foreach (var conflict in plan.Conflicts) DisableWithDependents(conflict.Id);
            foreach (var item in plan.Enable) { settings.EnabledMods.RemoveAll(id => id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)); settings.EnabledMods.Add(item.Id); }
        }
        else DisableWithDependents(mod.Id);
        settings.Save();
        }
        catch { settings.EnabledMods = previous; throw; }
        Changed?.Invoke();
    }
    void DisableWithDependents(string id)
    {
        var remove = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id }; bool changed;
        do { changed = false; foreach (var item in Enabled.ToList()) if (!remove.Contains(item.Id) && item.Dependencies.Any(d => remove.Contains(d.Id))) changed |= remove.Add(item.Id); } while (changed);
        settings.EnabledMods.RemoveAll(remove.Contains);
    }
    internal ModPlan PlanEnable(ModDefinition mod) => ModPlan.Build(this, new[] { mod }, Enabled);
    internal void SetEnabledSet(IEnumerable<ModDefinition> mods)
    {
        var plan = ModPlan.Build(this, mods, Array.Empty<ModDefinition>());
        if (plan.Errors.Count > 0 || plan.Conflicts.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, plan.Errors.Concat(plan.Conflicts.Select(m => "Conflicting mod: " + m.Name))));
        foreach (var item in plan.Enable) if (!Installed(item) || UpdateAvailable(item)) Install(item, false);
        settings.EnabledMods = plan.Enable.Select(m => m.Id).ToList();
    }
    internal void NotifyChanged() => Changed?.Invoke();
    internal void Uninstall(ModDefinition mod)
    {
        Enable(mod, false);
        // Only this manifest is removed. Keep the user's mod data for reinstall.
        string path = Path.Combine(Store, mod.Id, "manifest.json");
        if (File.Exists(path)) File.Delete(path);
        installed.Remove(mod.Id); Changed?.Invoke();
    }
    internal int Import(string path)
    {
        var incoming = new List<ModDefinition>();
        if (new FileInfo(path).Length > 16_000_000) throw new InvalidDataException("The package exceeds 16 MB.");
        if (path.EndsWith(".harbormod", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count != 1 || archive.Entries[0].FullName != "manifest.json" || archive.Entries[0].Length > 1_000_000) throw new InvalidDataException("A native mod package must contain one manifest.json file.");
            using var reader = new StreamReader(archive.Entries[0].Open()); incoming.Add(Read(reader.ReadToEnd()));
        }
        else
        {
            string content = File.ReadAllText(path);
            if (content.TrimStart().StartsWith('[')) incoming = JsonSerializer.Deserialize<List<ModDefinition>>(content, Json) ?? new();
            else incoming.Add(Read(content));
        }
        if (incoming.Count == 0 || incoming.Count > 10000) throw new InvalidDataException("A catalog can contain 1 to 10,000 mods.");
        foreach (var mod in incoming) Validate(mod); // Validate the entire catalog before changing anything.
        if (incoming.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != incoming.Count) throw new InvalidDataException("The catalog contains duplicate IDs.");
        if (incoming.Count == 1) Install(incoming[0], false);
        else { string dest = Path.Combine(Settings.DataDir, "Catalogs", "import-" + Guid.NewGuid().ToString("N") + ".json"); Atomic(dest, JsonSerializer.Serialize(incoming, Json)); }
        foreach (var mod in incoming) Upsert(mod);
        Sort(); Changed?.Invoke(); return incoming.Count;
    }
    internal static void Export(ModDefinition mod, string path)
    {
        Validate(mod);
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open()); writer.Write(JsonSerializer.Serialize(mod, Json));
    }
    internal void ReplaceCommunity(List<ModDefinition> incoming)
    {
        foreach (var mod in incoming) { Validate(mod); if (mod.Id.StartsWith("harbor.", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Community packages cannot replace built-in Harbor packages."); }
        if (incoming.Count > 10000 || incoming.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != incoming.Count) throw new InvalidDataException("Invalid community catalog IDs.");
        string path = Path.Combine(Settings.DataDir, "Catalogs", "community.json");
        var previous = File.Exists(path) ? JsonSerializer.Deserialize<List<ModDefinition>>(File.ReadAllText(path), Json) ?? new() : new();
        Atomic(path, JsonSerializer.Serialize(incoming, Json));
        var ids = incoming.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var old = previous.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Catalog.RemoveAll(m => old.Contains(m.Id) && !ids.Contains(m.Id) && !Installed(m));
        foreach (var mod in incoming) Upsert(mod); Sort(); Changed?.Invoke();
    }
    internal static ModDefinition Read(string json) { var mod = JsonSerializer.Deserialize<ModDefinition>(json, Json) ?? throw new InvalidDataException("Empty mod manifest."); Validate(mod); return mod; }
    internal static void Validate(ModDefinition mod)
    {
        if (mod == null || mod.Name == null || mod.Description == null || mod.Author == null || mod.Category == null || mod.Component == null || mod.Kind == null || mod.Author.Length > 120 || mod.Category.Length > 100) throw new InvalidDataException("Invalid mod metadata.");
        ModTaxonomy.Validate(mod);
        if (mod.Format is < 1 or > 3 || mod.Id == null || !Regex.IsMatch(mod.Id, @"^[a-z0-9][a-z0-9._-]{1,79}$")) throw new InvalidDataException("Invalid mod ID or package format.");
        if (string.IsNullOrWhiteSpace(mod.Name) || mod.Name.Length > 120 || mod.Description.Length > 8000 || !System.Version.TryParse(mod.Version, out _)) throw new InvalidDataException("Invalid mod metadata.");
        if (mod.Kind != "theme" && !(mod.Kind == "widget" && Components.Contains(mod.Component)) && !(mod.Kind == "tweak" && mod.Format == 3 && Tweaks.Components.Contains(mod.Component))) throw new InvalidDataException("This mod needs a component that Harbor does not support yet.");
        if (!System.Version.TryParse(mod.MinimumHarborVersion, out _) || mod.Tags == null || mod.Tags.Count > 30 || mod.Tags.Any(t => t == null || t.Length > 60) || mod.Dependencies == null || mod.Dependencies.Count > 30 || mod.Dependencies.Any(d => d == null || d.Id == null || !Regex.IsMatch(d.Id, @"^[a-z0-9][a-z0-9._-]{1,79}$") || !System.Version.TryParse(d.MinimumVersion, out _)) || mod.Conflicts == null || mod.Conflicts.Count > 100 || mod.Conflicts.Any(id => id == null || !Regex.IsMatch(id, @"^[a-z0-9][a-z0-9._-]{1,79}$"))) throw new InvalidDataException("Invalid compatibility or dependency metadata.");
        if (mod.Component == "dashboard") { if (mod.Format < 2 || mod.Layout == null) throw new InvalidDataException("Custom widget layouts require format 2 and a layout."); WidgetLayout.Validate(mod.Layout); }
        if (mod.Options == null || mod.Options.Count > 100 || mod.Values == null || mod.Values.Count > 200 || mod.Values.Any(v => v.Value == null || v.Value.Length > 10000)) throw new InvalidDataException("Invalid mod options.");
        if (mod.Options.Any(o => o == null || o.Key == null || o.Label == null || o.Default == null || o.Choices == null || !new[] { "text", "multiline", "number", "color", "choice" }.Contains(o.Type) || !Regex.IsMatch(o.Key, @"^[a-zA-Z][a-zA-Z0-9._-]{0,63}$") || o.Default.Length > 10000 || o.Label.Length > 300 || o.Choices.Length > 300 || o.Choices.Any(c => c == null || c.Length > 1000)) || mod.Options.Select(o => o.Key).Distinct().Count() != mod.Options.Count) throw new InvalidDataException("Invalid mod option keys or values.");
    }
    static void Atomic(string path, string value) => AtomicFile.WriteText(path, value);
    internal void ApplyTheme()
    {
        var theme = Enabled.LastOrDefault(m => m.Kind == "theme");
        string Color(string key, string fallback) => theme == null ? fallback : Value(theme, key, fallback);
        Theme.Palette(Color("background", settings.BackgroundColor), Color("panel", settings.PanelColor), Color("accent", settings.Accent), Color("text", settings.TextColor), Color("muted", settings.MutedColor), Color("raised", settings.RaisedColor), Color("border", settings.BorderColor));
    }
    static List<ModDefinition> Bundled()
    {
        var result = new List<ModDefinition>();
        Add("clock", "Desktop Clock", "Desktop", "A large desktop clock with your own date and time format.", Opt("format", "Time format", "HH:mm"), Opt("dateFormat", "Date format", "dddd, MMMM d"));
        Add("world-clock", "World Clock", "Desktop", "Keep another time zone on your desktop. Uses Windows time zone names.", Opt("zone", "Time zone", "Tokyo Standard Time"), Opt("format", "Time format", "ddd HH:mm"));
        Add("system", "System Monitor", "System", "CPU and memory usage with a live history graph.", Opt("history", "History in seconds", "60", "number"));
        Add("network", "Network Meter", "System", "Live upload and download rates from your active network adapters.");
        Add("storage", "Drive Space", "System", "Capacity and free space for your local drives.", Opt("drives", "Drive letters, comma separated", "C:,D:"));
        Add("uptime", "Uptime", "System", "See how long Windows has been running since its last full boot.");
        Add("notes", "Quick Notes", "Productivity", "An editable desktop note that saves locally as you type.", Opt("placeholder", "Note title", "Quick notes"));
        Add("tasks", "Checklist", "Productivity", "A desktop to-do list with saved checkboxes. Add one task per line in its settings.", Opt("items", "Tasks, one per line", "Make something useful\nTake a break", "multiline"));
        Add("focus", "Focus Timer", "Productivity", "A configurable work timer with pause, reset, and a notification when it finishes.", Opt("minutes", "Timer length in minutes", "25", "number"));
        Add("stopwatch", "Stopwatch", "Tools", "Start, pause, and reset a stopwatch without opening another app.");
        Add("calculator", "Desk Calculator", "Tools", "Evaluate arithmetic directly on your desktop with parentheses and decimals.");
        Add("converter", "Unit Converter", "Tools", "Convert temperatures, lengths, weights, speeds, and data sizes.");
        Add("folders", "Quick Folders", "Productivity", "Open your chosen folders in Harbor Files. Customize the paths and labels.", Opt("paths", "Folders, one per line", "%USERPROFILE%\n%USERPROFILE%\\Downloads\n%USERPROFILE%\\Documents", "multiline"));
        Add("clipboard", "Clipboard Shelf", "Productivity", "Keep recent copied text in memory. History clears when Harbor exits.", Opt("limit", "Items to remember", "12", "number"));
        Add("layouts", "Window Layouts", "Windows", "Keep Columns, Grid, Stack, and Undo within reach on the desktop.");
        Add("launchpad", "App Collection", "Productivity", "Make your own group of app and website shortcuts. One label | path per line.", Opt("apps", "Shortcuts", "Calculator | calc.exe\nNotepad | notepad.exe", "multiline"));
        foreach (var palette in new[] { ("midnight", "Midnight", "#111523", "#1A2034", "#9AAEFF"), ("forest", "Forest", "#111D1A", "#1A2C25", "#84D4AC"), ("ember", "Ember", "#211715", "#342420", "#FFB08A"), ("plum", "Plum", "#1C1525", "#2D2239", "#D0A3EE") })
            result.Add(new ModDefinition { Id = "harbor." + palette.Item1, Name = palette.Item2, Category = "Themes", Kind = "theme", Description = "A native color theme. Every color can be changed independently.", Options = new() { Opt("background", "Bar background", palette.Item3, "color"), Opt("panel", "Panel background", palette.Item4, "color"), Opt("accent", "Accent", palette.Item5, "color"), Opt("text", "Text", "#EDF1FA", "color") } });
        Dashboard("sound-desk", "Sound Desk", "System volume and media controls in one customizable panel.", new WidgetNode { Children = new() {
            Text("{volume:0}% volume", 28), new WidgetNode { Type = "progress", Source = "volume" },
            Row(Button("Quieter", "volume-down"), Button("Mute", "mute"), Button("Louder", "volume-up")),
            new WidgetNode { Type = "divider" }, Row(Button("Previous", "previous"), Button("Play / pause", "play-pause"), Button("Next", "next")) } });
        Dashboard("resource-desk", "Resource Desk", "A configurable system dashboard with CPU, memory, disk space, and uptime.", new WidgetNode { Children = new() {
            Text("CPU {cpu:0}%   RAM {ram:0}%", 23), Row(new WidgetNode { Type = "progress", Source = "cpu" }, new WidgetNode { Type = "progress", Source = "ram", Color = "#84D4AC" }),
            Text("C: {disk.C.free:0.0} GB free", 17), new WidgetNode { Type = "progress", Source = "disk.C.used" }, Text("Uptime {uptime}", 13), Button("Open Task Manager", "launch", "taskmgr.exe") } });
        Dashboard("day-planner", "Day Planner", "Combine a checklist, focus timer, and saved notes. Each part can be changed in the builder.", new WidgetNode { Children = new() {
            Text("{date:dddd, MMM d}", 22), Text("{time:HH:mm}", 34),
            new WidgetNode { Type = "component", Key = "tasks", Component = "tasks", Text = "Today", Values = new() { ["items"] = "Most important task\nTake a break" } },
            new WidgetNode { Type = "component", Key = "focus", Component = "focus", Text = "Focus", Values = new() { ["minutes"] = "25" } },
            new WidgetNode { Type = "component", Key = "notes", Component = "notes", Text = "Notes" } } });
        Dashboard("window-desk", "Window Desk", "App shortcuts, notifications, and window layouts in a single native dashboard.", new WidgetNode { Children = new() {
            Text("{windows} open windows", 22), Row(Button("Apps", "apps"), Button("Files", "files"), Button("Alerts", "notifications")),
            new WidgetNode { Type = "component", Key = "layouts", Component = "layouts", Text = "Layouts" }, Button("Show desktop", "desktop") } });
        result.AddRange(TweakPackages.All());
        foreach (var mod in result) { mod.Version = "1.1.0"; ModTaxonomy.Enrich(mod); }
        return result;
        void Dashboard(string id, string name, string description, WidgetNode layout) => result.Add(new ModDefinition { Format = 2, Id = "harbor." + id, Name = name, Category = "Dashboards", Description = description, Component = "dashboard", Layout = layout, Options = CommonOptions(360) });
        static WidgetNode Text(string text, double size) => new() { Type = "text", Text = text, FontSize = size };
        static WidgetNode Button(string text, string action, string argument = "") => new() { Type = "button", Text = text, Action = action, Argument = argument };
        static WidgetNode Row(params WidgetNode[] children) => new() { Type = "row", Children = children.ToList() };
        void Add(string component, string name, string category, string description, params ModOption[] options)
        {
            var opts = options.ToList(); opts.AddRange(CommonOptions());
            result.Add(new ModDefinition { Id = "harbor." + component, Component = component, Name = name, Category = category, Description = description, Options = opts });
        }
        static ModOption Opt(string key, string label, string value, string type = "text") => new() { Key = key, Label = label, Default = value, Type = type };
    }
    internal static List<ModOption> CommonOptions(int width = 280) => new() {
        new() { Key = "width", Label = "Widget width", Type = "number", Default = width.ToString() },
        new() { Key = "screen", Label = "Show on", Type = "choice", Default = "primary", Choices = new[] { "primary", "all", "secondary" } },
        new() { Key = "background", Label = "Widget background (blank to follow theme)", Type = "color" },
        new() { Key = "border", Label = "Widget border (blank to follow theme)", Type = "color" },
        new() { Key = "radius", Label = "Corner radius", Type = "number", Default = "14" },
        new() { Key = "opacity", Label = "Opacity, 0.2 to 1", Type = "number", Default = "1" }
    };
}
