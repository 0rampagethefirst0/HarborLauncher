# Harbor development

Canonical build: `App\Harbor.exe`. Preserve pins, startup, `%LOCALAPPDATA%\Harbor` data, and recovery when continuing.

Version 0.4.1 replaces the default menu gutter with a dark vector-icon menu template, adds vector icons and switches to Dock controls, and uses circular themed slider handles. Controls.xaml contains shared control templates; UiIcons.cs draws the icon paths without font dependencies. The existing 95-check suite passed; the visual check now renders 16 surfaces, including the actual dock context menu. Input/rule/recovery implementation is unchanged from 0.4.0.

Version 0.4.0 adds 12 native behavior packages, app/title pattern rules, dependency/conflict planning, catalog filters/favorites/updates, desktop profiles, and the requested hover-through dock. The real catalog contains 36 packages. Existing game hiding, persistent dock buttons, packaged app identity, software rendering, recovery, pins, startup, notes, and settings are preserved. The user confirmed v0.3 single-click minimize and restore.

Hover-through uses a layered dock with native transparent/no-activate styles and 45% default opacity over app content. The first right-button pair is consumed only to grant an interactive session; it does not open a menu or take focus. A later right-click follows normal menu handling. Open menus and flyouts keep the session alive, followed by a configurable three-second delay. Text-key activity through the existing keyboard hook resets the session on the typing app's monitor and maintains passive mode through a 1.5-second pause. No typed text is recorded. A single mouse hook serves all monitors; offscreen checks never install it. When enabled, the dock does not reserve bottom work-area space; the top bar still does. The reserved-strip behavior remains available in Appearance.

The overlap report exposed maximized apps retaining Windows' old work area. WindowSpace now refits maximized windows and fits ordinary windows on initial appearance, restore, or work-area changes. DWM frame insets are preserved; the maximized ApplicationFrameHost top resize border is accounted for. Fullscreen windows and games are excluded, and uncooperative windows are not repositioned continuously. Native taskbar broadcasts remain avoided.

Always-on-top and opacity use per-window recovery records guarded by PID and a window property token. Restore opacity before the final queued topmost change: writing the full extended style afterwards can otherwise undo that z-order restoration. WPF rejects external system-layered opacity; report and skip it. Test opacity with an owned native window and separately check normal WPF window behavior.

The running v0.2 shell exited after an access-denied recovery-state write. Atomic saves now use unique temporary files and File.Replace for an existing destination. Readers share write/delete access. Transient heartbeat locks are logged and retried rather than ending the shell. Initial restoration data must still be saved successfully before changing native UI. A live locked-file check confirmed the shell survived and heartbeat writes resumed after release.

Remaining work for the requested full shell:

- Author and validate the 1,000+ distinct useful mods. The real library is currently 36; a hosted marketplace is not implemented.
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
