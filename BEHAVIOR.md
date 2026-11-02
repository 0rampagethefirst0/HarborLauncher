# Behavior mods and profiles

Harbor's behavior mods add app-specific window rules, dock actions, toolbar controls, and per-app audio. They use format 3 `.harbormod` packages. All 12 are opt-in; existing widget/theme selections are preserved.

The small up-chevron centered above the dock opens its modifier box. It slides upward and contains exactly six controls: click-through, quiet while typing, faded opacity, active-after-leaving time, icon size, and motion duration. Changes apply immediately and save after a short debounce. The dock fades smoothly and interactive icons lift slightly on hover. Motion is brief and uses software rendering; zero duration disables it.

| Mod | What it changes |
| --- | --- |
| Always on Top | Keeps matching app windows above ordinary windows; restores their original state on disable or exit. |
| Window Opacity | Applies 0.5–100% opacity to compatible desktop windows and restores the original value. Fractional values are retained. Apps that manage their own transparency, including WPF windows, can decline the effect. |
| App Placement | Places matching windows on a selected monitor and screen region. Applied once per window; Undo layout restores the previous placement. |
| Remember Window Positions | Saves the last normal position of selected apps and restores it when they open again, constrained to current monitor bounds. |
| Scroll to Switch Windows | Cycles a running app's windows by scrolling its dock icon, with direction and minimized-window options. |
| Middle Click to Close | Closes a dock app's selected/recent window. App save prompts remain available. |
| Monitor-specific Dock | Limits pinned app indicators, switches, and menus to the dock's monitor. |
| Dock App Labels | Shows app names or window titles beside icons, with width and running-only options. |
| App Volume on the Dock | Alt + scroll adjusts the hovered app's audio sessions across active playback devices. |
| Scroll the Bar for Volume | Scroll adjusts system volume, optionally requiring Alt. |
| Toolbar Clock Format | Uses a custom date/time format, including seconds. |
| Active Window Controls | Double-click the toolbar title to maximize/restore; right-click it for window controls and app-specific mods. |

In hover-through mode, the first right-click activates the dock without opening a menu or taking keyboard focus from the app. A later right-click opens the normal app menu. The dock stays usable while the pointer is over it, while its context menu or a shell flyout is open, and for the configured delay after leaving. Otherwise hovering over app content beneath it fades the dock and passes clicks through. Activation consumes the right-button pair, so the underlying app does not receive that right-click too. Typing resets the interactive session and keeps the dock on that app's monitor in faded, click-through mode until 1.5 seconds after the last typing activity. Only activity times are retained; typed text is not recorded. Turn off this mode in Appearance to reserve a dedicated dock strip instead.

For an app rule, right-click its dock icon, choose **Mods for this app**, select a window mod, and choose **Create rule for this app**. This makes an independent local package. Edit settings, then **Install and enable** or **Enable**. App patterns accept executable names, full paths, and packaged app IDs; `*` matches any text and `?` matches one character. Add optional title patterns and exclusions, one per line. A blank app list matches nothing. Later enabled rules of the same type win for each window.

Window rules skip games and pause on the active game's monitor. Always-on-top and opacity restoration data is saved before changes and is also used by the recovery helper after a crash. Windows may reject effects on protected apps or apps that manage their own window styles; the mod's status reports this and unsupported effects are not repeatedly forced. Placement does not activate the app. Window memory saves normal positions at ten-second intervals and keeps up to 300 records.

The catalog shows minimum-version requirements, dependencies, and conflicts. Dependencies install/enable before their parent. Disabling a dependency also disables mods that require it. **Enable and replace** disables conflicting enabled mods before applying the selected mod. A second copy of a shell behavior conflicts with the first; app-specific window rules can coexist. **Updates** lists newer definitions in imported catalogs. Catalog fetching from a hosted marketplace is not implemented.

Profiles capture appearance, enabled package IDs, declared option values, and widget coordinates. Saving/exporting a profile excludes note contents, custom widget field data, clipboard history, pins, startup, and app position memory. Import adds a saved profile without applying it. Missing packages or incompatible/conflicting dependencies block application without changing the desktop configuration. After a successful switch, **Undo last switch** restores the previous setup. Profiles reference package IDs; export custom `.harbormod` packages separately when sharing a setup.

Harbor's packages do not execute downloaded scripts or Windhawk C++ modules. The behavior engine controls supported Windows features through their native APIs.
