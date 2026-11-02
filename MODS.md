# Harbor mods

Formats 1 and 2 use Harbor's native widget and theme engine. Format 3 adds native behavior mods, optional shell features and compatibility metadata. A `.harbormod` file is a ZIP containing one root file, `manifest.json`. JSON manifests can also be imported directly. No WebViews, downloaded scripts, DLLs, or game injection are loaded.

The 25 widget/theme packages are Desktop Clock, World Clock, System Monitor, Network Meter, Drive Space, Uptime, Quick Notes, Checklist, Focus Timer, Stopwatch, Desk Calculator, Unit Converter, Quick Folders, Clipboard Shelf, Window Layouts, App Collection, Midnight, Forest, Ember, Plum, Tokyo Dragon, Sound Desk, Resource Desk, Day Planner, and Window Desk. Twelve behavior packages and 20 optional shell features bring the actual library to 57; see [BEHAVIOR.md](BEHAVIOR.md).

Widgets support width, monitor choice, background, border, corner radius, opacity, and saved drag positions. Each functional widget has its own options. Clipboard text stays in memory for the session. Notes save locally. Export includes declared settings, excluding clipboard contents and note text.

Use **Create a plugin** to start from a package, customize its metadata and category, then save the independent plugin. Open its page to adjust settings or resume it. **Export** produces a shareable package; **Import** installs a package disabled. Catalog imports add available packages that run only after installation/enabling.

**Open visual builder** edits a composed layout or wraps an existing widget in one. Add, reorder, duplicate, and remove nodes in the outline; edit properties on the right; preview in the center. **Save as new** creates an independent local package. **Save changes** updates an existing local dashboard. The preview disables actions and saving widget data. Exporting a bundled mod from the builder gives it an independent ID.

Example manifest:

```json
{
  "format": 1,
  "id": "example.stream-clock",
  "name": "Stream Clock",
  "author": "Your name",
  "version": "1.0.0",
  "category": "Desktop",
  "description": "A clock for my second monitor.",
  "kind": "widget",
  "component": "clock",
  "options": [
    { "key": "format", "label": "Time format", "type": "text", "default": "HH:mm:ss" },
    { "key": "dateFormat", "label": "Date format", "type": "text", "default": "dddd, MMMM d" },
    { "key": "screen", "label": "Show on", "type": "choice", "default": "secondary", "choices": ["primary", "secondary", "all"] },
    { "key": "width", "label": "Width", "type": "number", "default": "300" },
    { "key": "radius", "label": "Corner radius", "type": "number", "default": "24" }
  ],
  "values": {}
}
```

IDs use lowercase letters, digits, dots, underscores, and hyphens, up to 80 characters. Versions are numeric `major.minor.patch`. Option types: `text`, `multiline`, `number`, `color`, and `choice`; defaults and saved values are strings.

Components: `clock`, `world-clock`, `system`, `network`, `storage`, `uptime`, `notes`, `tasks`, `focus`, `stopwatch`, `calculator`, `converter`, `folders`, `clipboard`, `layouts`, `launchpad`. Themes use `kind: "theme"` and colors `background`, `panel`, `accent`, and `text`.

Format 2 adds `component: "dashboard"` and a `layout` tree. Containers are `column`, `row`, and `wrap`. Leaf types are `text`, `progress`, `button`, `input`, `check`, `divider`, and `component` (an embedded native widget). Layouts allow up to 128 nodes and 10 nesting levels. Inputs, checks, and embedded widgets need distinct `key` values. Embedded dashboards cannot contain other dashboards.

For example, the following layout can be added to a format-2 widget manifest:

```json
{
  "type": "column",
  "gap": 10,
  "children": [
    { "type": "text", "text": "{time:HH:mm} • CPU {cpu:0}%", "fontSize": 24 },
    { "type": "progress", "source": "cpu", "maximum": 100 },
    { "type": "input", "key": "goal", "text": "Today's goal" },
    { "type": "check", "key": "done", "text": "Finished" },
    { "type": "button", "text": "Open files", "action": "files" }
  ]
}
```

Live text supports `{time:HH:mm}`, `{date:ddd}`, `{utc:HH:mm}`, `{uptime}`, `{cpu:0}`, `{ram:0}`, `{volume:0}`, `{windows}`, `{notifications}`, `{disk.C.free:0.0}`, `{disk.C.total:0.0}`, `{disk.C.used:0}`, `{window}`, `{mode}`, `{option.name}`, and `{field.key}`. Disk sizes are GB; `disk.C.used` is a percentage. Meters accept `cpu`, `ram`, `volume`, `windows`, `notifications`, and disk sources. Updates run once a second while that desktop is visible, with cached disk reads.

Button actions: `apps`, `files`, `mods`, `notifications`, `quick`, `appearance`, `desktop`, `columns`, `grid`, `stack`, `undo`, `launch`, `copy`, `mute`, `volume-up`, `volume-down`, `previous`, `play-pause`, and `next`. `argument` supplies a folder, launch path, or text; environment variables expand for files and launch. Field values save locally and are separate from declared options. Share manifests only from authors you trust: a launch button can open its configured application when clicked.

A catalog JSON file is an array of manifests, with up to 10,000 entries per import. The complete array is validated before library changes. Duplicate IDs, invalid metadata, unsupported components, path traversal, and extra ZIP entries are rejected. Installs are stored in `%LOCALAPPDATA%\Harbor\Mods`; settings are stored in `settings.json`.

Format 3 behavior manifests use `kind: "tweak"` and one supported `component`: `window-topmost`, `window-opacity`, `window-placement`, `window-memory`, `dock-scroll-windows`, `dock-middle-close`, `dock-monitor-only`, `dock-labels`, `dock-wheel-volume`, `toolbar-wheel-volume`, `clock-seconds`, or `title-actions`. Clone a bundled behavior package to see its declared settings. Behavior mods require Harbor 0.4.0 or newer.

All manifests can declare `minimumHarborVersion`, `tags`, `dependencies` (objects with `id` and `minimumVersion`), and `conflicts` (package IDs). Catalog entries do not automatically run. Import validates all entries before library changes; enabling checks compatibility, dependency cycles, missing/old dependencies, and conflicts.

The application library currently contains 56 packages. Large-catalog development fixtures stay in isolated test profiles and are excluded from the release library.


## Categories and previews (0.5)

Every new community package chooses exactly one `mainCategory`: `Desktop`, `Themes`, or `Tools`. `subcategory` must belong to that main category:

| Main category | Subcategories |
| --- | --- |
| Desktop | Windows, Dock, Toolbar, Dashboards |
| Themes | Dark, Light, Colorful, Minimal |
| Tools | Productivity, Monitoring, Time, Audio, Utilities, Shortcuts |

`category` remains for compatibility with older packages. Older manifests without the new fields are assigned a category automatically. Bundled definitions are upgraded without changing saved mod options. All 56 presets include useful tags; community tags are optional (up to 30, 60 characters each). Main category placement is independent of tags.

`previewImage` optionally embeds a `data:image/png;base64,...` or `data:image/jpeg;base64,...` banner, up to 500 KB and 4096 pixels per dimension. The creator resizes chosen images to 960 pixels wide. `previewAccent` optionally supplies a `#RRGGBB` color. Without an image, built-in packages use their individual recorded demonstration; custom widgets get a native layout snapshot. Preview images are part of the manifest, so exports are self-contained.

## Community

Browse and publish at https://harbor-mods.orangemole11.chatgpt.site. Public reads require no account; uploads require ChatGPT sign-in. **Upload to community** in Harbor saves the configured manifest under `%LOCALAPPDATA%\Harbor\Exports`, copies its path, and opens the upload page. Choose that JSON file (or an exported `.harbormod`), review its category/tags, and publish. The sign-in session stays in the browser.

Your account owns your uploaded IDs. Other accounts cannot overwrite them. Update a plugin by increasing its version in Harbor, exporting, and uploading the same ID. You can edit metadata or remove your own listing in Creator Studio. Existing installed copies remain local when a listing is removed. Harbor's refresh adds available community definitions without running them. Reserved `harbor.*` IDs cannot be uploaded or replaced by the feed.

The server validates native capabilities, metadata, image limits and layout bounds. Packages cannot add executable components to Harbor's engine. Custom widgets can use the documented native actions and layout builder; arbitrary scripts, DLLs and Windhawk injection are not supported. The catalog still needs more authors and capabilities to reach 1,000 distinct useful plugins.

The `Community` folder contains the Sites/Vinext source and D1 schema. `GET /api/catalog?page=0` returns up to 12 community manifests, `GET /api/download?id=...` downloads a manifest, and `/api/plugins` manages the signed-in creator's listings. The local HTTP verification script exercises a temporary preview-only package and removes it afterward.


## Demonstrations (0.5.1)

The built-in preview library has 56 posters and 56 frame sheets under `src/Previews`. `ModDemoScene.cs` stages desktop behavior without touching live windows; widgets use `Widget`/`WidgetLayout` with an explicit `WidgetDemoData` source. Calculator and converter examples run the actual expression and unit handlers. Sample input never writes settings, changes real audio or reads the user clipboard. Theme scenes use the package's palette on a whole shell view.

`Harbor.exe --render-demos <output-directory>` regenerates the media in an isolated profile and produces `demos.json` and a review sheet. Each film now has 96 frames in a 12-by-8 sheet, captured at 30 FPS. Films use 540?300 frames; posters stay 720?400. The desktop player decodes a film only when needed and allows one playing banner. The community site serves the same PNG media and stops its player on blur/hidden tabs; reduced-motion preferences keep static images. Custom uploads can provide their own PNG/JPEG banner.


Preview media is optimized to an indexed color palette after recording. The native and web players use elapsed time, so lower playback rates skip frames without slowing down the clip. `--optimize-demos <input> <output>` produces compact media; `--export-catalog <path>` exports fresh preset metadata without using the live profile.


## Optional shell features (0.10)

Use `kind: "feature"`, `format: 3`, minimum Harbor `0.10.0`, and a supported component: `taskbar`, `top-dock`, `taskbar-folders`, `media-controls`, `dock-clock`, `dock-stats`, `active-title`, `network-control`, `volume-control`, `dock-shortcuts`, `desktop-icons`, `file-manager`, `file-preview`, `file-thumbnails`, `collection-tabs`, `taskbar-dividers`, `dock-dividers`, `multi-monitor`, `notification-center` or `quick-settings`. These use Harbor's native capabilities; they do not execute downloaded code. Bundled names include Minimalists Dock, Bulky and Now Playing. Every optional feature starts disabled on fresh profiles; existing profiles migrate their current surfaces.

Compact tools support `snap`, `always-on`, `compact-format` and the existing access options. Snap choices include the four center-box corners, screen corners/midpoints, any edge and Free. Auto places Clock, System Monitor, Quick Folders and Drive Space around a large center box. `meter-mode` selects CPU + RAM, CPU or RAM; `drive-mode` selects All drives, First drive, Cycle drives or Used percent. Custom reading examples: `CPU {cpu:0}% / RAM {ram:0}%`, `{time:HH:mm:ss}`, `C: {disk.C.free:0} GB`. Always-on persists the expanded tool; game hiding remains per monitor.


## Visibility rules (0.11)

Preset tools add `visibility` (choice: `Always available`, `Apps screen only`, `With chosen apps`; default `Apps screen only`) and `visible-apps` (multiline, one executable name, full path or packaged app ID per line). Any selected app with an open or minimized window satisfies the rule. Empty lists satisfy no app rule; background tray processes alone do not count. Example list: `Discord.exe` then `opera.exe` on another line. Executable names survive version-folder changes; `*` and `?` wildcards are supported.

`always-on: Yes` keeps a tool expanded while its visibility rule matches. It does not override the rule or per-monitor game hiding. Hiding preserves the expanded tool and its settings; user close or Esc turns persistent expansion off. Each plugin can have its own list; several tools can be expanded together. Legacy custom manifests without a visibility option retain always-available behavior. Current built-in definitions are version 1.5.0 and require Harbor 0.11.0 for these controls.


## Harbor Home and frame rates (0.12)

The existing `harbor.desktop-shortcuts` feature is now **Harbor Home**. Its saved enabled state is preserved. It provides the central original folded blue emblem, desktop library and startup intro; Appearance controls the library/classic choice, emblem size/rotation and intro. Desktop sources use real existing shortcut paths. User categories and favorites are metadata and are not exported as plugin content.

Current bundled definitions are version 1.6.0. Widget and feature packages require Harbor 0.12.0. Native and public movie previews use 288 frames at 90 FPS; UI motion targets 120 FPS by default. Settings allow 80–144 FPS, and motion-off is preserved. Only the active preview runs, decoding remains serialized and timers stop when hidden or inactive.
