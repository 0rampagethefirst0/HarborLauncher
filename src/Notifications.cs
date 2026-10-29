using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;

namespace Harbor;

internal sealed record Notice(string Id, string App, string Title, string Body, DateTime Time);
internal sealed class Notifications
{
    readonly Settings config;
    readonly List<Notice> own = new();
    List<Notice> windows = new();
    internal List<Notice> Items => own.Concat(windows).Where(n => !config.DismissedNotifications.Contains(n.Id)).OrderByDescending(n => n.Time).Take(100).ToList();
    internal string SyncStatus { get; private set; } = "Reading Windows notifications…";
    internal event Action? Changed;
    DateTime nextPoll;
    int busy;
    string OwnPath => Path.Combine(Settings.DataDir, "notifications.json");
    internal Notifications(Settings config)
    {
        this.config = config;
        try { own = JsonSerializer.Deserialize<List<Notice>>(File.ReadAllText(OwnPath)) ?? new(); } catch { }
    }
    internal void Add(string title, string body)
    {
        own.Insert(0, new Notice("harbor-" + Guid.NewGuid().ToString("N"), "Harbor", title, body, DateTime.Now));
        if (own.Count > 100) own.RemoveRange(100, own.Count - 100);
        try { File.WriteAllText(OwnPath, JsonSerializer.Serialize(own)); } catch { }
        Changed?.Invoke();
    }
    internal void Dismiss(Notice notice) { config.DismissedNotifications.Add(notice.Id); Trim(); config.Save(); Changed?.Invoke(); }
    internal void DismissAll() { config.DismissedNotifications.AddRange(Items.Select(n => n.Id)); Trim(); config.Save(); Changed?.Invoke(); }
    void Trim() { config.DismissedNotifications = config.DismissedNotifications.Distinct().TakeLast(1000).ToList(); }
    internal async void Poll(bool force = false)
    {
        if ((!force && DateTime.UtcNow < nextPoll) || Interlocked.Exchange(ref busy, 1) != 0) return;
        nextPoll = DateTime.UtcNow.AddSeconds(15);
        try
        {
            var result = await Task.Run(ReadWindows);
            bool changed = !windows.SequenceEqual(result);
            windows = result; SyncStatus = "Windows notifications synced";
            if (changed || force) Changed?.Invoke();
        }
        catch (Exception ex) { SyncStatus = "Windows notifications unavailable"; if (force) Changed?.Invoke(); Log.Write("Notifications: " + ex.Message); }
        finally { Volatile.Write(ref busy, 0); }
    }
    static List<Notice> ReadWindows()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications\wpndatabase.db");
        var result = new List<Notice>(); if (!File.Exists(path)) return result;
        IntPtr db = IntPtr.Zero, stmt = IntPtr.Zero;
        try
        {
            if (Sql.Open(path, out db, 1, IntPtr.Zero) != 0) throw new IOException("Windows notification history is busy.");
            Sql.BusyTimeout(db, 100);
            const string query = "SELECT n.Id, h.PrimaryId, n.Payload, n.ArrivalTime FROM Notification n LEFT JOIN NotificationHandler h ON h.RecordId=n.HandlerId WHERE n.Type='toast' ORDER BY n.ArrivalTime DESC LIMIT 100";
            if (Sql.Prepare(db, query, -1, out stmt, IntPtr.Zero) != 0) throw new IOException("Unsupported Windows notification history format.");
            while (Sql.Step(stmt) == 100)
            {
                try
                {
                    long arrival = Sql.Int64(stmt, 3); DateTime time = DateTime.FromFileTimeUtc(arrival).ToLocalTime();
                    if (time < DateTime.Now.AddDays(-7)) continue;
                    int length = Sql.Bytes(stmt, 2); if (length <= 0 || length > 1_000_000) continue;
                    byte[] bytes = new byte[length]; Marshal.Copy(Sql.Blob(stmt, 2), bytes, 0, length);
                    string xml = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', '\0');
                    using var input = new StringReader(xml); using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
                    var document = XDocument.Load(reader);
                    string[] text = document.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value.Trim()).Where(s => s.Length > 0).Take(4).ToArray();
                    if (text.Length == 0) continue;
                    string app = Marshal.PtrToStringUTF8(Sql.Text(stmt, 1)) ?? "Windows";
                    if (app.Contains('!')) app = app.Split('!')[0].Split('_')[0].Replace("Microsoft.", "").Replace("Windows.", "");
                    result.Add(new Notice("windows-" + Sql.Int64(stmt, 0) + "-" + arrival, app, text[0], string.Join("\n", text.Skip(1)), time));
                }
                catch (Exception ex) when (ex is XmlException or ArgumentException) { }
            }
        }
        finally { if (stmt != IntPtr.Zero) Sql.Finalize(stmt); if (db != IntPtr.Zero) Sql.Close(db); }
        return result;
    }
    static class Sql
    {
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)] internal static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)] internal static extern int BusyTimeout(IntPtr db, int ms);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)] internal static extern int Prepare(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr stmt, IntPtr tail);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)] internal static extern int Step(IntPtr stmt);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)] internal static extern long Int64(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Text(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Blob(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)] internal static extern int Bytes(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)] internal static extern int Finalize(IntPtr stmt);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)] internal static extern int Close(IntPtr db);
    }
}

internal sealed class NotificationPanel : Window
{
    readonly Shell shell;
    readonly StackPanel items = new();
    readonly TextBlock status = Theme.Label("", 11, Theme.Muted);
    internal NotificationPanel(Shell shell, IntPtr monitor)
    {
        this.shell = shell; Title = "Harbor — notifications"; Theme.PopupWindow(this, 450, 660, monitor, false);
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; var close = Theme.IconButton("\uE8BB", "Close notifications", Close); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close); header.Children.Add(Theme.Label("Notifications", 24)); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0) }; var clear = Theme.Button("Clear", "Clear notifications from Harbor", shell.Notifications.DismissAll); DockPanel.SetDock(clear, System.Windows.Controls.Dock.Right); footer.Children.Add(clear); footer.Children.Add(status); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = Theme.Card(root, 18);
        shell.Notifications.Changed += Refresh; Closed += (_, _) => shell.Notifications.Changed -= Refresh; Theme.CloseOnDeactivate(this); Refresh(); shell.Notifications.Poll(true);
    }
    void Refresh()
    {
        items.Children.Clear(); status.Text = shell.Notifications.SyncStatus;
        foreach (var notice in shell.Notifications.Items)
        {
            var body = new StackPanel { Margin = new Thickness(13) };
            var meta = new DockPanel(); var dismiss = Theme.IconButton("\uE711", "Dismiss from Harbor", () => shell.Notifications.Dismiss(notice)); DockPanel.SetDock(dismiss, System.Windows.Controls.Dock.Right); meta.Children.Add(dismiss); meta.Children.Add(Theme.Label(notice.App + " · " + notice.Time.ToString("ddd h:mm tt"), 11, Theme.Muted)); body.Children.Add(meta);
            body.Children.Add(new TextBlock { Text = notice.Title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) }); body.Children.Add(new TextBlock { Text = notice.Body, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Theme.Muted });
            var card = Theme.Card(body); card.Margin = new Thickness(0, 0, 0, 9); items.Children.Add(card);
        }
        if (items.Children.Count == 0) { var empty = Theme.Label("You're all caught up.", 16, Theme.Muted); empty.Margin = new Thickness(0, 30, 0, 0); items.Children.Add(empty); }
    }
}
