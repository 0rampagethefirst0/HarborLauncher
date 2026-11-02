# Setup and recovery

## Requirements

Windows 10/11 x64 and the [.NET 9 Windows Desktop Runtime, x64](https://dotnet.microsoft.com/en-us/download/dotnet/9.0). The plain .NET runtime does not include WPF. Source builds require the .NET 9 SDK.

## First run

Extract the release ZIP. Keep `App`, `Session` and `Restore Windows.cmd` together inside the `Harbor` folder. Run `App\Harbor.exe` as your normal Windows user.

New profiles open the Features catalog. **Minimalists Dock** is the top bar, **Taskbar** is the app bar, **Bulky** adds taskbar folders and **Harbor Home** provides the central library. Files, media controls, dividers and tools are optional too. Existing profiles retain their choices.

Use Mods to install a theme or tool, read its instructions and adjust its options. Right-click surfaces to customize them. In hover-through taskbar mode, the first right-click activates it; the next opens the app's menu.

## Sign-in shell

Place Harbor in a permanent folder before enabling **Use Harbor as my desktop when I sign in** in Settings. Harbor backs up the prior per-user shell and folder-open registration and registers its supervisor. The supervisor waits for Harbor's desktops before retiring Explorer's desktop.

Restore Windows before moving the installation. Then run Harbor from the new location and enable sign-in replacement again.

## Return to Windows

- **Ctrl+Alt+F12** exits Harbor and restores Windows for the current session.
- **Restore Windows.cmd** also undoes the sign-in and folder-open registration. Run it before removing or moving Harbor.
- If Harbor is unavailable, open Task Manager with **Ctrl+Shift+Esc**, choose **Run new task**, and run `explorer.exe`. Run the independent `Restore Windows.ps1` from the latest backup under `%LOCALAPPDATA%\Harbor\ShellBackups` to undo registration.

Windows system files stay installed. User settings, plugins and backups remain in `%LOCALAPPDATA%\Harbor` after recovery.

## Update

Exit Harbor using the recovery shortcut. Extract the new release into the same installation folder, keeping its structure, then run `App\Harbor.exe`. Settings are stored separately. Source builds stage both executables before updating the local installation.

## Current boundaries

This is an early preview. Harbor reads available notification history, but Windows owns native toast popups. Quick settings can reveal the native tray for controls Harbor has not replaced yet. Security, sign-in, app dialogs and advanced OS settings remain Windows/app surfaces. The bundled library contains 57 packages.
