# Harbor Files

Folder refreshes update existing rows and keep the visible position and grid page. Wheel animation finishes at its destination; dragging the scroll thumb or using navigation keys cancels old wheel motion. Nested scroll areas use the wheel under the pointer.

Home and Frequently used are fixed navigation entries above a divider. The app opens on Frequently used by default. Home contains common places, pinned folders, app collections, recent folders and drives. Expand the drive tree to browse folders. Address entry accepts full paths, environment variables and UNC locations.

| Workflow | Controls |
| --- | --- |
| Navigation | Back/Forward, parent folder, breadcrumb buttons, Ctrl+L/Alt+D, Alt+Left/Right/Up, mouse back/forward buttons |
| Windows and tabs | Ctrl+N, Ctrl+T, Ctrl+W, Ctrl+Tab/Shift+Ctrl+Tab; folder menus open a new tab or window |
| Selection | Ctrl/Shift selection, Ctrl+A, More ? Invert selection, type to find an item |
| File operations | Ctrl+C/X/V, copy/move-to pickers, drag/drop; the Windows engine handles conflicts, permissions and transfer progress |
| Names and creation | F2, batch rename with preserved extensions, Ctrl+Shift+N, new text file, shortcut creation |
| Deletion | Delete recycles after confirmation; Shift+Delete requests permanent deletion after confirmation |
| Undo/redo | Ctrl+Z/Y and menus invoke the native shell's available session undo/redo command |
| ZIP | Compress selected items; Extract ZIP to a new folder. Traversal and link entries are rejected before extraction |
| Search | Current folder or subfolders; right-click search to choose scope. Wildcards such as *.txt and image?.png are supported |
| Views | Details/cards, zoom, hidden files, extensions, sort direction, grouping by type/date, resizable and selectable details columns |
| More actions | Alt+Enter/Properties, Open with, full native file menus, map/disconnect network drives |
| Recycle Bin | Real deleted items, original paths and dates, restore selected/all, confirmed emptying |

The full file menu exposes installed shell actions and extensions. Properties, Open with, native progress/conflict and network dialogs remain system-provided. Native undo availability depends on the shell session's undo provider; no action is fabricated when none is available. Archive helpers handle ZIP; installed archive applications remain available through Open with and shell menus. These controls cover common file-management workflows; no claim is made that every Explorer extension or specialist namespace has been reimplemented.

The file-operation checks use disposable fixtures in checks/. They do not rename, move or delete user files. The recycle round-trip returns its own fixture and verifies existing bin items remain present.
