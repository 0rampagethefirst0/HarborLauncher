# Harbor 0.7.0

A native Windows shell with a desktop, top Dock, app taskbar, Apps screen, file manager, notification center, settings, and native mods. Run **App\Harbor.exe** or the Harbor shortcut. Requires .NET 9 Windows Desktop Runtime x64.


The top **Dock** accepts drops across its whole width, including empty space and either side of an icon. Date and Time are independent pieces: drag Play between them. Right-click a piece to edit, duplicate, hide, or move it; use **Dock controls > + Piece** to add custom text, more clocks/time zones, app shortcuts, CPU/memory readings, or media controls. Each piece can have its own width and text size. Text supports `{date}`, `{time}`, `{cpu}`, `{ram}`, and `{title}`. Drag dividers and spaces as well.

The bottom **taskbar** supports app folders. Drop an app onto the center of another to group them; drop near an icon's edge to reorder. Click the folder to open its apps. Right-click to rename, move, or ungroup it. Drag more apps in, rearrange them inside, or drag an app back onto the taskbar. Every folder gets a shared tab and sidebar entry in **Files**; names and contents stay in sync. Files can add installed apps, move shortcuts between folders, and rearrange folder tabs. These folders organize shortcuts without moving or uninstalling programs.

- **Windows**, **Ctrl + Esc**, or **Ctrl + Alt + Space** opens apps. Focusing another app closes the Apps screen.
- **Win + E** opens Harbor Files; **Win + N** opens notifications; **Win + A** opens quick settings; **Win + D** shows/restores the desktop.
- **Ctrl + Alt + F12** exits and restores Windows. **Restore Windows.cmd** provides another recovery route.
- Right-click dock apps to pin, move, switch, close, or choose **Treat as game**. Drop an `.exe`, `.lnk`, or `.url` to pin it.
- Open **Mods** from the top bar or settings. Install/enable a package, edit its options, and choose **Apply settings**. Drag widgets by their headers. **Create a mod** makes an independent package from the selected mod and its settings; **Export** shares it as a `.harbormod` file.
- In Settings, choose **Customize colors, bars, and layout** to change colors, bar sizes, dock alignment, icon size, and top-bar controls. Themes can override the base palette.
- Select a mod and choose **Open visual builder** to compose rows, text, meters, buttons, saved inputs, checkboxes, and existing widgets. Edit the live preview, save a new package, or export it.
- **Hover-through dock:** when an app is behind the dock, hovering fades the dock and passes clicks to that app. The first right-click only activates the dock; another right-click opens its normal app menu. It stays interactive while you use it and for three seconds after leaving. Typing in an app returns that monitor's dock to faded, click-through mode until a short pause. Appearance controls the fade, delay, and switch back to reserved screen space. App menus include **Close windows** and **End task** for supported apps.
- **Behavior mods** change window, dock, toolbar, and audio behavior. Right-click a dock app and choose **Mods for this app** to create a targeted rule. See [BEHAVIOR.md](BEHAVIOR.md).
- **Profiles** save appearance, enabled mods, declared mod options, and widget positions. Switch profiles, export/import `.harborprofile` files, or undo the last switch. Pins, startup, notes, clipboard, and remembered app positions stay independent.
- Click the small **up-chevron at the dock's center** for a sliding six-setting box: click-through, typing mode, faded opacity, active time, icon size, and motion duration. Changes save automatically. Brief fades and icon lifts use software rendering; setting motion to zero disables them.
- Menus use native vector icons and dark rounded surfaces. Dock controls has matching icons, switches, and circular slider handles; the default white menu gutter and control boxes are replaced.

The dock now keeps each app button in place while window titles, focus, and running apps change. Packaged apps use their own Windows app identity instead of sharing a generic host-process group. Dock overflow scrolls with the mouse wheel, and **Show desktop** is available beside the layout control.

Harbor hides its bars only on the focused game's monitor, including windowed games. Other monitors keep Harbor visible. Hidden bars release their screen space, and cut-off game windows are fitted within the monitor without changing fullscreen mode. Override automatic detection with **Treat as game**. Native taskbar reappearance is handled immediately, and work-area changes avoid the broadcast that previously made taskbars alternate.

Harbor clears an inherited `ELECTRON_RUN_AS_NODE` flag before launching desktop apps. This fixes Medal silently exiting when Harbor was started by a development host. The system environment is unchanged.

Mods now has three main categories: **Desktop**, **Themes**, and **Tools**, each with subcategories. Choose a category for an animated showcase with white slide dots, previous/next controls, and a right arrow into its full collection. Cards have individual recorded demonstrations or a creator-supplied banner, tags, and a single **Install / Uninstall** button. Install enables the package immediately; uninstall keeps its saved settings. Details include settings, pause/resume, favorites, dependency/conflict information, and the visual builder.

**Create a plugin** starts from a working native capability. Choose one required main category and subcategory, select optional tags from 65 suggestions or enter your own, set a version, and attach a PNG/JPEG banner. Use **Upload to community** to prepare an export and open the public creator page; it copies the export path for the browser file picker. Sign in, choose the file, review it, and publish. Creator accounts can update and remove their own packages. Harbor fetches the community catalog when Mods opens and through its refresh button; downloaded definitions remain inactive until installed.

The [public Harbor Mods community](https://harbor-mods.orangemole11.chatgpt.site) includes a persistent upload service. The shipped library still contains **36 real packages**: 16 native widgets, four themes, four composed dashboards, and 12 behavior mods. The requested 1,000+ distinct mod library needs more actual plugins; the 1,200 scale-test entries remain isolated test fixtures. See [MODS.md](MODS.md) and [DEVELOPMENT.md](DEVELOPMENT.md).

Harbor combines a desktop shell with configurable behavior modules. Its packages run through Harbor's native engine. Windhawk `.wh.cpp` packages are not compatible with Harbor's package format.

Harbor Files supports navigation, folder search, new folders, rename, copy, cut/paste, and recycling. The notification center reads available Windows toast history without modifying the database; dismissing affects Harbor's view. Windows toast popups, advanced OS settings, app-owned dialogs, sign-in, and security screens remain Windows/app-owned. Quick settings temporarily reveals the native system tray while Harbor's tray is developed.

WPF software rendering is requested before creating windows and on each render target. Panels are solid; wallpaper is static. The desktop shell has no browser renderer, blur, or live wallpaper. Motion uses short native UI transitions and the active category slideshow; it stops while the catalog is inactive and respects the motion-off setting. Windows still uses its compositor.

A separate watchdog restores taskbars, desktop icons, and saved monitor working areas after exit, failure, or an unresponsive UI. Its 20-second timeout excludes sleep/hibernation. Recovery-state writes tolerate temporary file locks and retry on the next heartbeat. Explorer remains running to host the desktop and system services. Preferences, notes, mods, and logs live in `%LOCALAPPDATA%\Harbor`.

Build with `powershell -ExecutionPolicy Bypass -File .\build.ps1`; add `-Install` for shortcuts. A successful build is staged before the live shell is restarted. Use `-BuildOnly` to stage a build without restarting Harbor. Source is included under `src`.

- `Harbor.exe --verify <directory>` runs isolated package, widget, settings, file-operation, and game-rule checks.
- `Harbor.exe --render-check <directory>` renders 16 offscreen previews without changing the desktop.
- `Harbor.exe --status --output <file>` writes runtime status.
- `--files`, `--mods`, `--notifications`, `--settings`, `--appearance`, `--profiles`, `--dock-controls`, and `--launcher` open the corresponding surfaces.

To remove Harbor, exit it, disable **Start Harbor when I sign in**, and remove its shortcuts/project folder. Release ZIPs exclude personal settings and screenshots.


In 0.5.1 all 36 bundled packages have their own demonstration: the 20 widgets/dashboards use the actual native controls with deterministic sample data, the four themes show the whole shell palette changing, and each of the 12 behavior mods has its own staged desktop example. Hover a banner to play it; the featured/detail preview starts automatically. Only one preview plays at once, playback stops when the catalog loses focus, and motion zero keeps posters static. Example windows and values are labeled as previews; they do not manipulate your real apps, clipboard, audio or saved data.


## 0.6 changes

Previews run at **30 FPS** with 96 recorded frames per clip. Captions and preset descriptions now explain the action and result in plain language. Playback stays limited to one active preview and stops when Harbor loses focus; choose 12?30 FPS in Dock controls.

**Harbor Files** has tabs, clickable breadcrumbs, an editable path, icon grid and details views, folder-first sorting, search per tab, pinned folders, and image/text previews. Large grids show 200 items per page; details remain virtualized. Copy, cut, paste, rename, new folders, drag/drop, and recycling remain available. Transfers show progress and can stop after the current item. Existing names are never overwritten silently. Ctrl+T opens a tab, Ctrl+W closes it, Ctrl+Tab switches tabs, Ctrl+L edits the path, Ctrl+F searches, and Alt+Left/Right navigates history.

The **Dock is the top bar**. Click its small downward arrow or right-click it for Dock controls. Buttons can be dragged, moved between left/center/right groups, or hidden. Add lines or spaces between them. The Buttons, Style, and Behavior pages control sizing, spacing, rounding, opacity, borders, clock options, visibility, monitor placement and screen-space reservation. You can restore a hidden Dock from the bottom taskbar?s context menu or Harbor settings. Desktop profiles include these choices.

The bottom **taskbar** keeps its existing click-through and typing behavior. Right-click an app to add a divider before or after it. Right-click the divider again to change it to a space or remove it.
