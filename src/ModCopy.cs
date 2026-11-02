using System.Collections.Generic;

namespace Harbor;

internal static class ModCopy
{
    static readonly Dictionary<string, (string Description, string Caption)> Copy = new() {
        ["clock"] = ("Keep the time in a small control. Expand it for the date and your chosen clock format.", "A small clock expands into the full date and time."),
        ["world-clock"] = ("See the time in another city without changing your PC’s time zone.", "Check Tokyo’s time alongside your local time."),
        ["system"] = ("Watch CPU and memory usage. The graph shows recent CPU activity.", "The graph tracks CPU use. RAM use stays beside it."),
        ["network"] = ("See how fast files are downloading and uploading right now.", "See download and upload speeds separately."),
        ["storage"] = ("See how much space is free on each drive you choose.", "Check the free space on each drive."),
        ["uptime"] = ("See how long Windows has been running since the last full restart.", "See the time since your last Windows restart."),
        ["notes"] = ("Open a quick note when you need it, then collapse it. Your text saves as you type.", "Expand Notes, write, then collapse. Your text stays saved."),
        ["tasks"] = ("See task progress in a small control. Expand it to check things off; progress survives a restart.", "Expand the checklist, tick a task, then keep just the progress visible."),
        ["focus"] = ("Start a work timer. Pause, reset, or get a notification when time is up.", "Start a countdown for your next focus session."),
        ["stopwatch"] = ("Time an activity from zero. Pause or reset whenever you need.", "Count up from zero, then pause or reset."),
        ["calculator"] = ("Type a calculation and see the answer immediately. Parentheses work too.", "Type (12 + 8) × 4. The answer appears: 80."),
        ["converter"] = ("Convert temperatures, distances, weights, speeds, and data sizes.", "Convert 100 km/h into 62.14 mph."),
        ["folders"] = ("Keep your favorite folders one click away. They open in Harbor Files.", "Click Projects to open that folder."),
        ["clipboard"] = ("Keep recently copied text nearby. Click an item to copy it again. History clears when Harbor closes.", "Copy several things, then pick an earlier item."),
        ["layouts"] = ("Arrange open windows into columns, a grid, or a stack. Undo restores their previous positions.", "Choose a layout to organize your open windows."),
        ["launchpad"] = ("Keep app, folder and website shortcuts behind a small movable control.", "Expand your shortcuts and choose an app or website."),
        ["sound-desk"] = ("Expand a small sound control to adjust volume, mute audio, and control music.", "Open sound controls, change volume, then collapse them."),
        ["resource-desk"] = ("See CPU, RAM, drive space, and uptime together in one panel.", "Check CPU, RAM and free space in one place."),
        ["day-planner"] = ("Keep your tasks, focus timer, and notes together. Change the layout in the builder.", "Use your checklist, timer and notes together."),
        ["window-desk"] = ("Open apps, files, notifications, and window layouts from one panel.", "Open apps and organize windows from one panel."),
        ["window-topmost"] = ("Keep chosen apps above other windows, even while you work in another app.", "Focus another app. Your pinned window stays in front."),
        ["window-opacity"] = ("Make chosen app windows partly transparent so you can see what is behind them. Some apps manage their own transparency.", "Lower opacity to see the files behind Notepad."),
        ["window-placement"] = ("Choose where an app opens: a monitor, side, corner, or the center of the screen.", "Open Notepad on the right side automatically."),
        ["window-memory"] = ("Remember where you put an app. Reopening it restores that position. Games are skipped.", "Move it, close it, then reopen it in the same place."),
        ["dock-scroll-windows"] = ("Scroll over a taskbar app to switch between its open windows.", "Scroll this icon to switch between three windows."),
        ["dock-middle-close"] = ("Middle-click a taskbar app to close one of its windows. Save prompts still appear.", "Middle-click to close one window, leaving the other open."),
        ["dock-monitor-only"] = ("Each taskbar shows running indicators and window menus for its own display.", "Each taskbar marks the apps open on its own display."),
        ["dock-labels"] = ("Show app names or window titles next to taskbar icons. Choose how wide labels can be.", "See app names beside their taskbar icons."),
        ["dock-wheel-volume"] = ("Hold Alt and scroll over a taskbar app to change only that app’s volume.", "Alt + scroll lowers Music. Game volume stays at 70%."),
        ["toolbar-wheel-volume"] = ("Scroll on the top Dock to change system volume. You can require Alt to avoid accidental changes.", "Scroll the top Dock to change system volume."),
        ["clock-seconds"] = ("Choose the date and time format shown in the top Dock. Add seconds if you want.", "Add seconds to the clock on the top Dock."),
        ["title-actions"] = ("Double-click the active app title in the Dock to maximize it. Right-click for window actions.", "Double-click to maximize. Right-click for window controls."),
        ["midnight"] = ("A deep blue theme. Change any color to suit your desktop.", "Apply Midnight across your Dock, taskbar and widgets."),
        ["forest"] = ("A dark green theme with soft mint accents. Every color is editable.", "Apply Forest across your Dock, taskbar and widgets."),
        ["ember"] = ("A warm dark theme with peach accents. Every color is editable.", "Apply Ember across your Dock, taskbar and widgets."),
        ["plum"] = ("A dark purple theme with lilac accents. Every color is editable.", "Apply Plum across your Dock, taskbar and widgets.")
    };
    static string Key(string id) => id.StartsWith("harbor.") ? id[7..] : id;
    internal static string? Description(string id) => Copy.TryGetValue(Key(id), out var value) ? value.Description : null;
    internal static string? Caption(string id) => Copy.TryGetValue(Key(id), out var value) ? value.Caption : null;
}
