## Version 0.14.2

ScrollMotion owns each axis and commits attached offsets with SetValue before releasing animation clocks. The former SetCurrentValue plus FillBehavior.Stop invalidation restored the attached property's default zero. Wheel targets are axis-specific; native navigation stops unfinished motion, nested viewers own their wheel gestures, and unloaded viewers release clocks. ScrollPosition keeps refresh offsets after layout and cancels stale restores on input, including already-handled wheel events. LiveItems reconciles watched file lists without replacing their source or unnecessarily rebuilding grouping.

Dock and taskbar controls share compact rows, grouped sections and fading advanced expanders. Numeric transparency accepts 0.5 percent and half steps through persistence and native opacity; activation holds accept 500 ms. Legible content sizes and frame targets retain their existing bounds.

Theme.CurrentColors is an immutable snapshot safe for the startup dispatcher. Frozen 3D material palettes and wallpaper caches include the theme colors. Preset themes expose ten color roles; custom plugin color overrides remain explicit. DragonArt projects front/back outline layers and their connecting faces in software. DragonHomeFire uses three bounded vector flame layers and nine embers; Home crossfades the saved idle pose, performance and prepared library. The desktop anchor remains frozen until closing restores its exact pose.

The fire and card share one interpolated 48-point contour. Filled flame tongues morph into Home's rounded surface and cool into the exact panel color before its contents appear. ProfileHeading accounts for the drawing's muzzle slope so the head and flame point upward at 17 degrees in side profile. Closing restores the saved desktop pose. DragonFrameCache prepares 160 cropped, frozen frames on an STA worker and materializes pixels before playback. ArrangeDragon branches before the normal card layout, and a panel-colored curtain fades off the final opaque layout. Rendering remains software-only.

Validation includes 345 native checks and 15 timed scroll checks, including real Mods detail updates, FileSystemWatcher refreshes and grid pagination in concealed owned windows. The release build passed both suites after the final curtain optimization. The scroll harness observes native foreground events so external app switches are recorded separately from test-window activation. The 83-view offscreen review includes controls, dragon rotations and fire phases. Scrolling was also confirmed on the installed build. A pre-curtain dragon run averaged 78 animation updates per second; these are application timings, not presented-frame measurements or an 80 FPS guarantee. Computer Use screenshot capture and coordinate input were unavailable, so no complete mouse-driven UI pass is claimed.

## Version 0.13

DesktopBin uses SHQueryRecycleBin for the indicator. RecycleBin reads the Shell namespace on STA workers, and restores only selected canonical item paths. Files.Recycle renders dedicated deleted-item columns and keeps its item container in details mode independently of the user's ordinary folder view. A refresh timer updates the actual contents without replacing an unchanged list.

HomeLayout owns bounded layout preferences, tab order/names/visibility and local usage history. The two fixed tabs and divider are always present. HomeSettings edits a draft, commits only its declared preferences, and opens Home after its closing fade has completed. Profiles include appearance preferences and omit activity history. Home preloads the actual starting tab's icons and delays snapshot caching until tab fades finish.

WindowFade handles entry, exit and reversible visibility transitions. Modal results are preserved through delayed closing. Its live probe uses only cloaked, owned windows and verifies intermediate opacity, final visibility, pointer release, reversal, modal confirmation and unchanged foreground. ElementFade handles desktop widgets. Native popup placement no longer requests premature showing. Home retains its separate first-paint and source-pose handoff.

Tokyo Dragon uses an embedded outline PNG plus DragonArt's shared vector renderer. Theme.Skin switches the emblem, icon resource and backdrop cache key. Artwork applies before startup presentation. Older cached catalog records cannot overwrite newer bundled definitions. Presets are 1.7.0 and require Harbor 0.13.

ShellFileOps wraps IFileOperation on STA workers. ShellFileMenu hosts IContextMenu, including extension message handling; it rejects mixed-parent selections. Files.Actions adds native operations, common keyboard commands, search, archives and metadata actions. Managed ZIP helpers validate every extraction path before creating output and preserve partial extraction on cancellation. File details carry creation/attribute metadata instead of reading it during sorting.

Validation: 331 native checks; six live fade lifecycle checks; 70 offscreen views; live Home/Frequent, Home settings and 43-item Recycle Bin checks. All three shell recovery paths and the taskbar's pinned-only preference are preserved. Desktop input control remains unavailable because the native pipe cannot connect; the UI review uses rendered artifacts and application commands, not a mouse-driven pass.

## Home opening blink correction

Home no longer uses SWP_SHOWWINDOW during native handle creation. Its own native window is cloaked for initial paint, with native entrance/exit transitions disabled so they do not overlap Harbor's animation. ContentRendered and a one-time DwmFlush establish the first prepared frame before removing the cloak. A three-second paint timeout closes an unfinished Home and releases its desktop anchor.

The desktop emblem stays frozen and visible underneath Home for the whole handoff. Animation clocks retain the current reveal value while starting, then commit the destination only at completion; they no longer set the fully open base value before the first animation tick.

The build passes 312 native checks and 25 targeted Home checks. Three live openings reported first-frame-ready, applied and released cloaking, zero initial reveal and zero animation-start reveal, then a usable Home. Settings and taskbar preferences are preserved. This confirms the presentation lifecycle; it is not a manual visual confirmation of the reported blink.

API references: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute and https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.contentrendered

## Applied Home and taskbar polish

The current build is installed. Home keeps a single visible emblem through opening, the settled header and closing. Text stays at native scale, revealed inside the growing card. The mesh uses beveled faces and fixed lighting without a glow plate or edge outlines. The recorded Home demo now uses the real Home view throughout, avoiding a separate introductory logo and an obvious handoff.

Opening loads desktop metadata and icons before Show, captures the laid-out panel, and starts motion after the first layout and foreground work. The full-monitor wallpaper stays static. Companion controls wait until Home settles; heavy periodic polling pauses during the morph while foreground game handling and recovery stay active. Closing retains a warmed panel snapshot and returns to the frozen desktop position and rotation.

The taskbar defaults to pinned apps and folders. Taskbar controls can opt into Show open apps. Disabling it removes automatic running-app buttons and their separators without changing pins, folder contents or saved divider positions. Profiles include this preference. Runtime status reports the setting and the actual running-button count per monitor.

Native checks pass in checks/polish-final-native/verification.json. The offscreen visual review renders 64 views in checks/polish-visual-reviewed. Its isolated profile installs the enabled built-in manifests before rendering, so optional Dock pieces and app folders are represented accurately. The targeted Home review is in checks/home-polish-review-final. These checks do not establish a completed mouse-driven review: the connected desktop-control host still returns a missing native-pipe error.

Motion targets 120 FPS in software rendering. One live Home opening measured 68.4 UI updates per second, with a 29.1 ms p95 and no gaps over 50 ms. This is a limited timing sample, not evidence of an 80 FPS minimum or physical display presentation rate.

## Motion and screenshot corrections

HarborBackdrop caches frozen software-rendered wallpaper images; the desktop no longer inherits the old wallpaper by default. Home captures its laid-out panel once for the morph and uses render transforms for the flying emblem, restoring interactive controls after the transition. Desktop ticks still run during games so they can stop covered emblem clocks. AnimationTiming pairs process timer-resolution requests only while visible motion runs. FrameTiming reports actual UI update intervals, including p95 and frames over 25/50 ms. Presentation FPS remains dependent on the compositor and display.

Screenshots is a shell-owned on-demand GDI capture service. Keyboard consumes Win+Shift+S and Print Screen variants before forwarding Windows chords. CaptureSelection freezes the desktop before selection, uses virtual-screen pixel coordinates, and cancels without saving on Esc/right-click. Completed regions are copied with bounded clipboard retries and saved as PNGs. Shortcut routing, negative-monitor clipping and PNG pixel roundtrips have fixture checks. Physical shortcut and display smoothness checks are reported separately.

The independent startup dispatcher gives Skip intro its local Style before Content initializes it; otherwise the main thread's mutable Button style can be resolved across threads. A hidden two-monitor probe with Theme installed guards this startup regression. Runtime restarts skip the intro when a game is focused.

# Harbor development

Version 0.12 introduces HarborEmblem, DesktopPortal, DesktopLibraryModel, HarborHome and StartupPresentation. The emblem is an original three-piece folded gate, with extruded polygon faces, perspective projection, back-face culling and blue lighting. It uses a small software-drawn mesh rather than a Viewport3D, browser or shader. The desktop portal stops its clock when hidden or covered. Home's clip and transforms expand from the clicked emblem into the categorized library, and reverse on explicit close. External app focus closes it immediately. It follows the existing desktop-icons feature ID, now named Harbor Home.

DesktopLibraryModel enumerates the real user/common desktop without a 150-item truncation, and uses the app index for All apps. Categories and favorites are metadata; categorizing never moves shortcut files. The responsive grid adjusts page size to available height, and supports searching, favorites, custom groups, drag-to-category and taskbar pinning. Classic icons remain available through Appearance. Home participates in Apps-only visibility and the existing transient-focus watcher.

StartupPresentation runs self-contained artwork on a separate STA dispatcher and owns its resources, avoiding cross-thread theme brushes. Core readiness controls the outro after a bounded intro, with Skip and an eight-second fallback. Disposal and startup exceptions remove overlays, and the supervisor/watchdog recovery path remains unchanged. Explicit --preview-intro runs the standalone intro and can report frame timing; --intro plays it in the running shell. Before the pacing corrections, the redesigned intro averaged approximately 94 UI-rendered FPS at a 120 FPS target. The current code removes full-window transparency during startup, adds bounded high-resolution scheduling, and exposes frame interval statistics. Final physical pacing and shortcut checks await a desktop test; Rocket League stayed focused during installation.

Animations migrate to a 120 FPS target, previews to 90 FPS, with a requested 80 FPS minimum. The 56 movies contain 288 frames each; their 6480×7200 indexed sheets keep full-frame geometry while bounding active preview decoding. A preview-only pulse drives WPF rendering at the UI target and stops when playback stops. New Home/intro settings are captured in desktop profiles. Static diagnostics include nine emblem, startup and morph poses; no live settings are changed by those renders.

Version 0.11.1 adds QuickSettingsMotion for the control settings panel. Event-driven WPF animation clocks handle a 220 ms entry, 140 ms exit, bounded page crossfades/slides, a moving tab highlight and expandable app input. Transitions use Motion.Animation's configured frame target and cubic easing, and respect global motion-off. Page changes retain only one outgoing view; rapid changes cancel older transitions. Closing finishes once, while disposal cancels and detaches all clocks. Expanded input animates a clipping container so TextBox minimum height does not prevent a smooth collapse. No bitmap caches, blur, GPU effects or idle animation loops are introduced.

Validation: 262 native checks passed. Eight new checks advance the real WPF clocks to verify intermediate entry/page/expansion states, interruption, final sizing, latest-page interaction, single close completion, cleanup and the immediate fallback for hidden or disabled motion. Fifty-four offscreen surfaces still render without live settings changes. Runtime health and settings preservation are verified after installation; physical frame pacing remains a separate user check.

Version 0.11 introduces PluginVisibility and PluginQuickSettings. Twenty preset tools declare visibility (Always available, Apps screen only or With chosen apps) and a multiline visible-apps list. The native engine matches open WindowTracker entries, including minimized windows, by executable name, full path or packaged app identity. Any matching rule enables that tool; empty lists match nothing. The picker uses stable executable names across app updates, handles common updater shortcuts, and supports several selections. Legacy custom plugins without declared visibility keep their previous availability.

PluginQuickSettings replaces the floating control's right-click menu and desktop widget controls with Visibility, Position and Appearance pages. Changes stay in a draft until Save. App-specific visibility defaults to persistent expansion; desktop-only presentation switches to floating when needed. Persistent panels hide and resume without losing expansion preferences or contents. Rules apply to compact handles, full desktop panels, plugin Dock pieces and expanded popups; per-monitor game hiding takes precedence. Collapsed Dock pieces are excluded from layout measurement.

Launcher defers external-focus dismissal to Shell's foreground watcher so Harbor tool interactions can keep Apps open. ShowPluginPanel retains Apps for tool flyouts; ordinary system flyouts keep their previous behavior. Opening a tool directly brings up Apps when its rule requires it. Live state records rule matches and visible/total controls separately.

Validation: 254 native checks pass, including multiple app matches, independent rules, empty lists, packaged identities, wildcard bounds, version-stable executable names, legacy defaults, several selections, draft/cancel preservation and hidden Dock layout. Fifty-four surfaces render with SoftwareOnly; four cover the new settings pages. Native IPC checks confirmed Apps reveals its controls, tool expansion keeps Apps open, and closing Apps hides controls and closes the transient tool. Physical right-click feedback is recorded separately.

Version 0.10 adds ShellFeatures, PluginSnapping, SnapGuides, PluginReadingEditor, TaskbarTransparency and DividerEditor. Twenty Format 3 feature manifests gate the taskbar, Minimalists Dock, Bulky, Now Playing, Files and other surfaces. First-run profiles enable no optional packages and open the Features catalog. Existing profiles migrate legacy booleans to enabled feature IDs. Turning features off keeps saved data; Bulky's folder pins flatten visually while disabled. Dependencies enable required surfaces and disable dependents when a surface is paused.

The center snap box spans 60% of the usable monitor area, with 20% margins. Four default tools use its corners; the remaining tools line up along edges. Guides exist only during dragging, accept no input and reserve no space. Always-on panels are independent of transient flyouts, persist their mode, and hide only on their own game monitor. CPU/RAM and drive modes use the existing readings; custom compact text resolves the established data tokens.

Taskbar opacity derives separately from base, hover and activated transparency (5-95%). Typing selects hover mode and retains click-through. DividerVisual gives thin lines a minimum 20-pixel target; Alt-drag adjusts width, ordinary drag moves, and DividerEditor exposes spacing/thickness/length plus directional movement. Default taskbar separators convert into movable custom separators on their first move. An open divider editor preserves the interactive lease while allowing live layout refresh.

Validation: 235 native checks pass, including migration, fresh defaults, dependencies, CPU/RAM, drives, custom time, snapping, transparency and saved always-on behavior. Forty-nine offscreen surfaces cover the new editors, snap layout and fresh feature catalog. All 56 packages have 192-frame demonstrations at 60 FPS. Live state and settings migration are checked separately from physical mouse interaction.

The v0.9 tool presentation layer is in PluginPresentation, PluginControls and PluginHandle. Presets default to independent edge tabs; Dock placement is an optional access style. The desktop only renders tools explicitly set to Desktop panel. PluginControls owns one background Widget per compact tool so CPU history, focus completion and clipboard capture survive collapse. A popup reparents that view and detaches it on close; notes flush only dirty changes. Nested dashboard components use inline views rather than repeated cards and drag headers. Custom legacy packages without access options retain desktop behavior.

PluginHandle uses the existing first-click activation path, no appbar/work-area reservation, per-monitor game hiding, bounded software animations and collision reflow for edge dragging. Managed plugin Dock entries are synchronized only for explicit Dock access; manually duplicated pieces retain their own placement. Catalog merges ignore older versions so a cached community catalog cannot replace newer bundled access controls or usage guides. Preview scenes use real compact controls and real tool bodies with a staged expand/collapse interaction at 60 FPS.

Canonical build: `App\Harbor.exe`. Preserve pins, startup, `%LOCALAPPDATA%\Harbor` data, and recovery when continuing.

Version 0.4.1 replaces the default menu gutter with a dark vector-icon menu template, adds vector icons and switches to Dock controls, and uses circular themed slider handles. Controls.xaml contains shared control templates; UiIcons.cs draws the icon paths without font dependencies. The existing 95-check suite passed; the visual check now renders 16 surfaces, including the actual dock context menu. Input/rule/recovery implementation is unchanged from 0.4.0.

Version 0.4.0 adds 12 native behavior packages, app/title pattern rules, dependency/conflict planning, catalog filters/favorites/updates, desktop profiles, and the requested hover-through dock. The real catalog contains 36 packages. Existing game hiding, persistent dock buttons, packaged app identity, software rendering, recovery, pins, startup, notes, and settings are preserved. The user confirmed v0.3 single-click minimize and restore.

Hover-through uses a layered dock with native transparent/no-activate styles and 45% default opacity over app content. The first right-button pair is consumed only to grant an interactive session; it does not open a menu or take focus. A later right-click follows normal menu handling. Open menus and flyouts keep the session alive, followed by a configurable three-second delay. Text-key activity through the existing keyboard hook resets the session on the typing app's monitor and maintains passive mode through a 1.5-second pause. No typed text is recorded. A single mouse hook serves all monitors; offscreen checks never install it. When enabled, the dock does not reserve bottom work-area space; the top bar still does. The reserved-strip behavior remains available in Appearance.

The overlap report exposed maximized apps retaining Windows' old work area. WindowSpace now refits maximized windows and fits ordinary windows on initial appearance, restore, or work-area changes. DWM frame insets are preserved; the maximized ApplicationFrameHost top resize border is accounted for. Fullscreen windows and games are excluded, and uncooperative windows are not repositioned continuously. Native taskbar broadcasts remain avoided.

Always-on-top and opacity use per-window recovery records guarded by PID and a window property token. Restore opacity before the final queued topmost change: writing the full extended style afterwards can otherwise undo that z-order restoration. WPF rejects external system-layered opacity; report and skip it. Test opacity with an owned native window and separately check normal WPF window behavior.

The running v0.2 shell exited after an access-denied recovery-state write. Atomic saves now use unique temporary files and File.Replace for an existing destination. Readers share write/delete access. Transient heartbeat locks are logged and retried rather than ending the shell. Initial restoration data must still be saved successfully before changing native UI. A live locked-file check confirmed the shell survived and heartbeat writes resumed after release.

Remaining work for the requested full shell:

- Author and validate the 1,000+ distinct useful mods. The real library is currently 56. A public community catalog and authenticated upload service are deployed; the requested 1,000+ library still needs distinct useful packages.
- Extend supported behavior capabilities and richer authoring beyond the current native components. Windhawk `.wh.cpp` packages are not compatible.
- Replace Windows toast popups and provide supported live notification delivery/actions. Current Windows-history sync is a read-only database adapter with an unavailable-state fallback.
- Implement native tray icons and device/network/display settings. Advanced configuration currently opens Windows settings or reveals its tray.
- Replace remaining shell dialogs where Windows permits it. Sign-in/security desktops remain OS-owned.
- Broaden real packaged-app compatibility tests beyond the native property-store checks and ordinary process identities.
- Broaden physical hover-through, app-menu, drag/drop, rapid switching, and audio-scroll checks across apps. v0.3 minimize/restore was user-confirmed; the new right-click session needs its own acceptance.

Preserve user-confirmed single-click/no-boost behavior. Hide bars only on the game's monitor. Avoid global work-area broadcasts that make Explorer redraw its taskbar. Record original areas and native icon visibility in recovery state before changes.

The centered dock chevron opens DockControls with six settings. Opening uses a short slide/fade; dock opacity and icon hover use bounded animations with no idle loop. Motion duration zero disables them. The small panel applies changes without replacing dock buttons and debounces saves. Offscreen construction never starts animations or changes input hooks.

`--verify` runs 95 checks in an isolated profile, including a 1,200-entry test catalog, native identity stores, persistent buttons, layout validation, profiles, dependency plans, native window effect restoration, dock activation/typing leases and native pass-through styles, six-control construction, and selected-process audio changes using an owned silent stream. `--render-check` renders 16 real control surfaces offscreen. Neither replaces physical clicks or game testing. Computer Use's native pipe could not connect (OS error 2); native status, controlled fixtures, renders, and user reports supply the evidence.

`tools\RuntimeCheck` builds an owned test window named RocketLeague.exe to exercise the real foreground event and game rules. Its live checks cover per-monitor bars, work areas, game-frame bounds, Apps dismissal, borderless mode, monitor moves, ordinary-app focus, maximize preservation, and native taskbar hiding. It does not modify or terminate a user's game. Separate neutral test modes provide WPF and native windows; native mode is used for opacity restoration because WPF rejects that external style.

`tools\test-behavior-recovery.ps1` stages a supplied build, temporarily enables three rules targeting only its owned native fixture, verifies placement/effects and exit/crash restoration, then restores the exact saved settings and removes its temporary manifests. It also checks native taskbars, icons, work areas, enabled mods, startup, and pins. Personal backup/status files remain local and are excluded from release ZIPs.

Build changes stage successfully before replacing App files; `build.ps1 -BuildOnly` leaves the running shell untouched. Release ZIPs include source, binaries, tools, documentation, and sanitized check summaries; they exclude screenshots, test profiles, preferences, and user data.


## 0.5 catalog and community

The catalog navigation, category showcases, paginated banner cards, category/tag schema, and plugin creator are native WPF controls. `ModTaxonomy.cs` preserves legacy manifests and classifies all 36 presets; `ModBanner.cs` draws previews without WebViews. At most 12 catalog cards are created per page. The seven-second showcase timer stops on deactivation; motion zero disables automatic rotation and transitions.

`CatalogVerification.cs` adds 14 checks to the existing 95: category migration/validation, invalid images, slide controls, real Install/Uninstall clicks and settings retention, and atomic community cache replacement. Total: 109 checks. Diagnostics renders 21 actual surfaces, including all three category showcases, the card catalog and creator. The latest render preserved the live settings hash. This does not substitute for physical mouse/gameplay acceptance.

The community service uses Sites hosting and D1, with sign-in-owned writes, origin checks, bounded request bodies, a per-creator daily upload limit, reserved built-in IDs, native manifest validation and no automatic execution. Eighteen local HTTP checks cover upload/update/download/removal, authorization boundaries and malformed packages. TypeScript and production builds pass. Connected Computer Use is unavailable, so browser visual/interactive QA remains unverified.

Release checks record 0.5 native/offline, visual, HTTP and live shell status separately. The 0.4 game-layout/recovery/heartbeat reports remain prior baseline evidence; those disruptive checks are not rerun for the catalog-only change. Full Windows UI replacement and 1,000 distinct mods remain wider project work.


## 0.5.1 preview correction

Generic label illustrations are replaced by unique demonstrations for every preset. Widget demonstrations render the real controls using isolated sample data. Desktop demonstrations stage the supported operation (placement, stacking, transparency, closing, monitor filtering, labels, independent/system volume, clock formatting, title actions). The native player and public site share the exact 72 media files.

Seven additional checks cover all 72 resource dimensions, calculator output, real unit conversion controls, checklist changes, editor input, distinct behavior scenes, and absence of settings/notification writes. Total: 116 passing native checks. All 36 posters were reviewed together, along with the rendered catalog and showcases. Live mouse playback remains unverified because connected Computer Use is unavailable.


## 0.6 Dock and Files

`BarLayout.cs` stores ordered top-Dock items, enabled state, group alignment, lines/spaces, and taskbar divider anchors. Legacy visibility choices migrate once. `TopBar.cs` only rebuilds its layout when its specification changes, defers during its own clicks/drags/menus, and detaches reused label content first. `TopDockControls.cs` provides Buttons/Style/Behavior pages. Disabling reservation or hiding the Dock releases its work area; primary-only mode does not reserve the other monitor. Existing per-monitor game hiding and restoration remain in place.

`Files.cs` now uses `FileTab` state, a virtualized details presenter, paged icon cards and an asynchronous preview pane. File commands preserve the existing clipboard/recycle behavior, support drag/drop, and reject self-transfers or existing destinations. Operation errors remain visible. `ShellUiVerification.cs` adds 23 checks covering migration, reorder/save, profile data, monitor-space choices, tabs, sort/search, file transfers, actual directory loading and text previews. Total: 139 native checks. Visual checks render 27 surfaces, including all Dock control pages, both Files views, and an optimized movie frame.

Previews have 96 frames at 30 FPS, with elapsed-time playback and one active movie. Palette optimization shrinks the film library from about 46 MB to 9 MB. The players stop on focus loss and respect motion preferences. Physical drag interaction and live frame timing remain unverified because the Computer Use connection is unavailable.


## 0.7 dynamic Dock pieces and shared app folders

DockPlacement.cs replaces narrow drop zones with a full-width panel and a visible landing outline. Positions are stored as fractions of available width; collision handling keeps icons on screen, and overflow scrolls. A drop commits the visible positions, while cancellation leaves preferences unchanged. The suite covers 630 placements across widths and edges. Legacy groups remain available as alignment shortcuts.

Date and Time migrate out of the combined Clock without enabling hidden controls. BarItem stores the component, custom text/format, time zone, target, width, text size, and free position. DockPieceEditor supports independent duplicates. Desktop profiles include the layout schema version and piece data. Existing clicks, media actions, per-monitor hiding, software rendering and recovery remain in place.

TaskbarFolders stores named virtual folders and app shortcuts in settings. Center drops create/add to folders; edge drops reorder shortcuts. FolderUi and AppFolderPopup show mosaic icons and app actions. Shared changes update the taskbars and Files tabs immediately. Files.AppFolders adds live folder tabs, sidebar entries, app picking, reordering and cross-folder moves. Disk copy/recycle controls are hidden in app folders. Ungrouping restores the member shortcuts. Normalization preserves the existing Pins list object and app targets.

Native verification: 169 passing checks. Visual review: 32 offscreen surfaces, with unchanged live settings. Computer Use confirmed placing Play between Date and Time in the isolated real-control fixture; the user stopped Computer Use with Escape before the physical folder drag test. No further computer-control actions were taken. Folder flows are covered by native action/model/view checks, with physical folder dragging still unverified.


## 0.8 readability, guidance and motion

Taskbar folders use a larger leading icon, secondary cover, count badge and visible name, with per-user size controls and enough taskbar height. Existing names and members are preserved; newly grouped apps receive a descriptive suggested name. File card sizing and text are adjustable. Files.Home adds virtual Home navigation, quick places, live app collections, recent folders and drive space. Filled folder art, bounded asynchronous image thumbnails, larger details rows, labeled actions and a useful unselected inspector replace the sparse 0.7 layout. Disk operations and app-folder shortcut operations remain separate.

ModHelp supplies concrete steps for all 36 presets and units/examples for option fields. The instructions field is validated, exported, editable in the creator and supported in the community schema/editor. Theme presets no longer export an empty tag.

Films are 192 frames at 60 FPS in a 6480 by 4800 indexed PNG, with one native movie active at a time. Native playback uses CompositionTarget.Rendering; decode occurs on a background thread, and generation checks discard stale requests. Web playback uses requestAnimationFrame and a media-version cache key. Motion adds bounded eased hover, navigation, reflow and scrolling. SoftwareOnly remains enforced; animation targets are configuration, not measured physical display FPS.

Validation: 188 native checks and 36 rendered surfaces; isolated rendering preserved the live settings hash. New checks cover background film decoding, guide export/validation, all preset instructions, folder tile dimensions/captions, Home navigation/history, labeled actions, useful inspector data and immediate-close zoom persistence. Community checks cover all guides, legacy/default handling, length limits, creator roundtrip, and all 36 film dimensions. Physical timing and live mouse workflows were not automated in this change.
