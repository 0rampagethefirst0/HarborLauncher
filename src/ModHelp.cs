using System;
using System.Collections.Generic;
using System.Linq;

namespace Harbor;

internal static class ModHelp
{
    static readonly Dictionary<string, string[]> Guides = new() {
        ["clock"] = new[] { "Install to place a clock on your desktop. Use Win + D to see it behind your apps.", "Drag the widget header to move it. In settings, HH:mm means 24-hour time; h:mm tt means 12-hour time.", "Choose a separate Date format, then click Apply settings." },
        ["world-clock"] = new[] { "Install to add another clock to your desktop; your Windows time zone stays the same.", "Set Time zone to a Windows zone name, such as Tokyo Standard Time or Eastern Standard Time.", "Choose a time format and click Apply settings. Drag the header to place it beside your local clock." },
        ["system"] = new[] { "Install, then look on your desktop for the CPU and RAM panel.", "The CPU line updates while apps work; RAM shows the amount of memory currently in use.", "Change History in seconds to show a shorter or longer graph. Click Apply settings." },
        ["network"] = new[] { "Install to show a network panel on your desktop.", "Start a download to see the download rate rise. Upload and download are shown separately.", "Drag the panel by its header and change its width in settings." },
        ["storage"] = new[] { "Install to show free space for your chosen drives on the desktop.", "Enter drive letters separated by commas, for example C:,D:. Only available drives are shown.", "Click Apply settings. This panel reports space; it does not clean or delete files." },
        ["uptime"] = new[] { "Install to show the time since Windows last completed a full boot.", "Read days, hours, minutes, and seconds directly on the desktop.", "Drag its header to move it; change Width to give it more room." },
        ["notes"] = new[] { "Install and find Quick Notes on your desktop.", "Click inside the note and type. Changes save automatically on this PC.", "Set Note title to rename the panel. Drag the header to move it." },
        ["tasks"] = new[] { "Install to put a checklist on the desktop.", "In Tasks, enter one task per line, then click Apply settings.", "Click a checkbox to complete a task. Your checked items are kept when Harbor restarts." },
        ["focus"] = new[] { "Install to add a countdown timer to your desktop.", "Set Timer length in minutes, click Apply settings, then press Start on the widget.", "Pause holds your remaining time. Reset returns to the full session. Harbor adds a notification when it finishes." },
        ["stopwatch"] = new[] { "Install, then find Stopwatch on the desktop.", "Press Start to count upward from zero; press Pause to hold the reading.", "Press Reset to return to zero. Drag the header to keep it within reach." },
        ["calculator"] = new[] { "Install to add the calculator to your desktop.", "Type a calculation such as (12 + 8) * 4 in its input. The result updates as you type.", "Use +, -, *, /, decimals, and parentheses. Replace the input to start another calculation." },
        ["converter"] = new[] { "Install and find Unit Converter on your desktop.", "Choose the conversion type and the input/output units, then enter a number.", "For example, choose speed, km/h to mph, and enter 100. The converted result appears immediately." },
        ["folders"] = new[] { "Install to add folder shortcuts to the desktop.", "In Folders, enter one folder path per line. %USERPROFILE%\\Downloads opens your Downloads folder.", "Click Apply settings, then click a shortcut in the widget to open it in Harbor Files." },
        ["clipboard"] = new[] { "Install to add Clipboard Shelf to the desktop.", "Copy some text in another app. It appears as a new item in the shelf.", "Click an older item to copy it again, then paste it where needed. History stays in memory and clears when Harbor exits." },
        ["layouts"] = new[] { "Install to add window layout buttons to the desktop.", "Open a few ordinary app windows, then choose Columns, Grid, or Stack.", "Choose Undo to restore their earlier positions. Active games and full-screen windows are skipped." },
        ["launchpad"] = new[] { "Install to add a desktop group of shortcuts.", "In Shortcuts, use one Label | path per line. Example: Calculator | calc.exe or Website | https://example.com.", "Click Apply settings. Click a named button in the widget to open its app, folder, or website." },
        ["sound-desk"] = new[] { "Install to add the Sound Desk panel to your desktop.", "Use its volume, mute, previous, play/pause, and next buttons to control system audio and media.", "Open the visual builder to rearrange the controls or change the panel layout." },
        ["resource-desk"] = new[] { "Install to place CPU, RAM, drive space, and uptime in one desktop panel.", "Read the meters while your apps run. Each section uses live system readings.", "Use the visual builder to resize, rearrange, or remove sections." },
        ["day-planner"] = new[] { "Install to put your checklist, focus timer, and notes together on the desktop.", "Check off tasks, start a focus session, and type directly into the notes area.", "Use the visual builder to rearrange sections and adjust their sizes." },
        ["window-desk"] = new[] { "Install to add a desktop panel for Apps, Files, notifications, and window layouts.", "Click a named control to open that Harbor surface. Choose a layout to organize open app windows.", "Use the visual builder to add, remove, or rearrange controls." },
        ["window-topmost"] = new[] { "Install, then enter the apps that should stay above other windows. The default example matches notepad.exe.", "Use one executable name per line, such as Discord.exe. Optional title patterns narrow the match.", "Click Apply settings and open a matching app. Pause or uninstall the plugin to restore normal stacking." },
        ["window-opacity"] = new[] { "Install, then put the target executable names in Apps, one per line.", "Set Opacity percent: 100 is solid; 70 lets you see through the window. Click Apply settings.", "Open a matching app to try it. Some apps manage their own transparency; the live status below explains skipped effects. Pause restores the original opacity." },
        ["window-placement"] = new[] { "Install and choose which executable names to match in Apps.", "Choose a monitor and screen region. Center width/height are percentages; Edge gap is in pixels.", "Click Apply settings, then open a matching app. Placement is applied once for that window. Use Undo layout to restore its previous position." },
        ["window-memory"] = new[] { "Install and enter the apps whose window positions should be remembered.", "Click Apply settings, open a matching app, and move or resize its ordinary window.", "Close and reopen that app to restore the remembered position. Active games are skipped." },
        ["dock-scroll-windows"] = new[] { "Install, then open two or more windows of the same app.", "Activate the taskbar with a right-click if it is faded, then scroll over that app's icon to switch windows.", "Choose whether minimized windows join the cycle and which scroll direction to use." },
        ["dock-middle-close"] = new[] { "Install to change middle-click behavior on the bottom taskbar.", "Activate the taskbar if it is faded, then press the mouse wheel over an app icon to close one window.", "If the app has unsaved work, its own save prompt still appears. Pause the plugin to restore the usual middle-click action." },
        ["dock-monitor-only"] = new[] { "Install on a setup with two or more monitors.", "Each taskbar's running dots and window menu now refer to windows on that display.", "Pinned shortcuts remain available. An app open only on another display can open another instance when clicked here." },
        ["dock-labels"] = new[] { "Install to show readable text beside bottom-taskbar app icons.", "Choose app names or window titles, and whether labels show always or only for running apps.", "Set Maximum label width in pixels, then click Apply settings." },
        ["dock-wheel-volume"] = new[] { "Install, then play audio in the app you want to adjust.", "Activate the bottom taskbar if it is faded. Hold Alt and scroll over that app's icon.", "Only that app's active audio sessions change. Volume step percent controls the amount per scroll notch." },
        ["toolbar-wheel-volume"] = new[] { "Install, then scroll on the top Dock to change system volume.", "Set Require Alt to yes if you want the shortcut to work only while Alt is held.", "Change Volume step percent and click Apply settings to choose the amount per notch." },
        ["clock-seconds"] = new[] { "Install to show seconds in the top Dock's Time piece.", "Date and Time are separate pieces. Right-click either and choose Edit piece for its own format and time zone.", "For a combined Clock piece, this plugin's format controls both date and time. Example: ddd, MMM d HH:mm:ss. Click Apply settings." },
        ["title-actions"] = new[] { "Install and make sure the Active window piece is enabled in Dock controls.", "Double-click the active app title in the top Dock to maximize or restore its window.", "Right-click that title for minimize, close, always-on-top, and app-specific plugin actions." }
    };
    internal static string Build(ModDefinition mod)
    {
        string key = mod.Id.StartsWith("harbor.") ? mod.Id[7..] : mod.Component;
        if (Guides.TryGetValue(key, out var steps))
        {
            if (mod.Kind != "widget") return string.Join("\n", steps);
            return string.Join("\n", new[] { "Install, then open Harbor's Apps screen to find the small " + PluginPresentation.ShortName(mod) + " control. Click it to expand the tool; click away or press Esc to collapse." }
                .Concat(steps.Skip(1).Select(s => s.Replace("Drag the widget header to move it. ", "").Replace("Drag the header to place it beside your local clock.", "").Replace("on the desktop", "in the expanded tool").Replace("directly on your desktop", "in the expanded tool").Replace("Drag its header to move it; change Width to give it more room.", "Change Width to give the expanded tool more room.").Replace("Drag the header to move it.", "").Replace("Drag the header to keep it within reach.", "").Trim()))
                .Concat(new[] { "Right-click the control for Visibility, Position and Appearance. Choose Apps screen only, Always available, or With chosen apps; choose several apps and keep the tool expanded while any is open. Drag to snap around the center box or any edge. Games still hide tools on their monitor." }));
        }
        if (mod.Kind == "theme") return "Click Install to apply this palette to Harbor's Dock, taskbar, widgets, and windows.\nChange individual colors below; use a hex color such as #8AB4FF, then click Apply settings.\nPause or uninstall the theme to return to your base appearance. Your saved theme settings are kept.";
        return "Install, then use the plugin's compact control to expand its tools. Older custom plugins can keep their desktop placement.\nUse the named controls, and change Access style below with Apply settings.\nChoose Edge tab, Floating control, Dock button, or Desktop panel. Drag a compact control to move it; click away to collapse its tool.";
    }
    internal static string[] Steps(ModDefinition mod) => (string.IsNullOrWhiteSpace(mod.Instructions) ? Build(mod) : mod.Instructions).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    internal static string OptionHint(ModDefinition mod, ModOption option) => option.Key switch {
        "visibility" => "Apps screen only shows this tool with Harbor's Apps menu. With chosen apps shows it while any app in your list has a window open, including minimized windows. Always available keeps its compact control accessible.",
        "visible-apps" => "One executable name, full path or packaged app ID per line. Example: Discord.exe. Any chosen app can activate the rule. Use Control settings for a searchable app picker.",
        "snap" => "Auto spreads Clock, System Monitor, Quick Folders and Drive Space around a large center box. Choose a box corner, screen side, corner or Free to place it yourself.",
        "always-on" => "Keep this tool expanded while its visibility rule matches. Apps screen only hides it when Apps closes; With chosen apps hides it when every chosen app closes. Its mode survives restarts. Esc or close turns expansion off.",
        "compact-format" => "Combine readings with tokens such as CPU {cpu:0}% · RAM {ram:0}% · {time:HH:mm} · C: {disk.C.free:0} GB. Leave blank for the tool's mode.",
        "meter-mode" => "Show CPU, RAM or both in the same compact control.",
        "drive-mode" => "Show all selected drives, just the first, a rotating drive every four seconds, or used-space percentages.",
        "access" => "Choose independent edge access, a freely movable control, an optional Dock button, or a full desktop panel. Content and settings stay with the plugin.",
        "edge" => "The side of the screen used by an Edge tab. Drag the control vertically to place it.",
        "compact-reading" => "Show a short live reading, just the tool's name, or an icon alone. Hovering still shows the full name and reading.",
        "control-size" or "control-width" => "Size of the small collapsed control in pixels; the expanded tool keeps its own Width setting.",
        "transition-ms" => "Opening animation duration: 180 is a short smooth transition. The global motion-off setting disables it.",
        "control-x" or "control-y" => "0 is the start of the available screen space; 1 is the end. Leave blank for automatic placement, or simply drag the control.",
        "apps" => mod.Kind == "tweak" ? "One executable name or pattern per line, for example Discord.exe. * matches any text." : "One Label | path per line, for example Calculator | calc.exe.",
        "titles" => "Optional window-title patterns, one per line. Leave empty to match any title in the selected apps.",
        "exclude" => "Apps or window-title patterns to skip, one per line.",
        "width" or "height" => mod.Component == "window-placement" ? "Percentage of the chosen display when using Center." : "Size in pixels. Increase this if the panel feels cramped.",
        "monitor" => "Choose the display on which the panel or window should appear.",
        "format" => "HH:mm = 24-hour time; h:mm tt = 12-hour time; add :ss for seconds.",
        "dateFormat" => "ddd, MMM d = Thu, Oct 29; dddd, MMMM d shows the full day and month.",
        "zone" => "A Windows time-zone name, such as Tokyo Standard Time or Eastern Standard Time.",
        "paths" => "One folder path per line. %USERPROFILE%\\Downloads works for your own Downloads folder.",
        "items" => "One task per line. Each line becomes a separate checkbox.",
        "minutes" => "Length of a focus session, in minutes.",
        "drives" => "Comma-separated drive letters, such as C:,D:.",
        "step" => "Percentage points added or removed for each scroll notch.",
        "opacity" => "100 is fully solid. Lower values make the target window more transparent.",
        "gap" => "Empty space between the window and display edge, in pixels.",
        "history" => "How many seconds of CPU activity the graph keeps visible.",
        "limit" => "Maximum number of copied text items to keep during this Harbor session.",
        _ => option.Type == "color" ? "Use a hex color, for example #8AB4FF." : option.Type == "choice" ? "Choose a value, then click Apply settings." : "Change the value, then click Apply settings."
    };
}
