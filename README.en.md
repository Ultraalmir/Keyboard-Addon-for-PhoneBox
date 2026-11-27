# PhoneBox Keyboard Add-on

Independent Windows companion for navigating PhoneBox pages with global keyboard shortcuts. It does not patch, inject into, or replace PhoneBox files.

![Version](https://img.shields.io/badge/version-1.5.0-6478f5) ![Platform](https://img.shields.io/badge/platform-Windows%2011%20%7C%20supported%2010%20LTSC-0078D4) ![License](https://img.shields.io/badge/license-MIT-green)

🌐 **Project website:** [ultraalmir.github.io/Keyboard-Addon-for-PhoneBox](https://ultraalmir.github.io/Keyboard-Addon-for-PhoneBox/) · [Latest download](https://github.com/Ultraalmir/Keyboard-Addon-for-PhoneBox/releases/latest)


## Features

- Up to **10 configurable direct page shortcuts** for the first ten PhoneBox page positions.
- Separately configurable **Next page** and **Previous page** actions that traverse every enabled page, including pages beyond position 10.
- Key-combination capture from the local HTML settings panel.
- Global and per-action enable/disable switches.
- Bilingual settings panel with a Portuguese (Brazil) / English selector; the selected language is saved and retained across restarts and updates.
- Silent background operation: no console or main window; the notification-area icon is optional and controlled from the HTML panel.
- Optional per-user Windows startup shortcut.
- GitHub Releases update checker and assisted incremental updater with SHA-256 verification, executable backup, and rollback; settings, shortcuts, and logs are preserved.
- Self-contained Windows x64 executable; end users do not need to install the .NET runtime.

## Default shortcuts

| Action | Default shortcut | Initially enabled |
|---|---|---|
| Pages 1–7 | `Ctrl + Numpad 1` through `Ctrl + Numpad 7` | Yes |
| Page 8 | `Ctrl + Numpad 8` | No |
| Page 9 | `Ctrl + Numpad 9` | No |
| Page 10 | `Ctrl + Numpad 0` | No |
| Next page | `Ctrl + Numpad +` | Yes |
| Previous page | `Ctrl + Numpad -` | Yes |

All twelve actions can be configured independently in the settings panel. Each hotkey requires at least one modifier (`Ctrl`, `Alt`, `Shift`, or `Win`). Windows can refuse a combination already registered by another application; the panel reports registration conflicts.

Keep NumLock on when using numeric Numpad keys. The add-on uses the Windows `RegisterHotKey` API and Windows virtual-key codes, so special keys can behave differently depending on keyboard firmware.

## Next/Previous navigation

PhoneBox **0.9.31** adds the official `POST /api/goto/next` and `POST /api/goto/prev` webhooks. The add-on sends these commands directly to the PhoneBox agent, which uses the page actually displayed on the phone and enforces the list boundaries: navigation stops at the first and last pages instead of wrapping around.

This keeps navigation synchronized after touch, swipe, PhoneBox rules, or another controller changes the page. PhoneBox 0.9.31 or later is required for these shortcuts.

**Per-phone page profiles:** this version reads the global page list from `/api/config` without an `X-PhoneBox-Device` header. If you configured different page lists under PhoneBox → Settings → Phones, verify that the global list matches the connected panel; automatic per-phone profile selection is not included yet.

## Automatic updates

The settings panel checks stable GitHub Releases. When a newer version is available, it can download the official package, verify its SHA-256 checksum, and replace only the installed executable. The add-on closes and restarts automatically; `settings.json`, logs, and Start Menu shortcuts remain untouched. Automatic updates require an installation created by the project installer.

## Requirements

- Windows 11 x64 on a release listed as supported by .NET 10.
- Windows 10: Enterprise/LTSC editions only, on releases that remain within Microsoft's support lifecycle and are compatible with .NET 10. See the [.NET compatibility matrix](https://learn.microsoft.com/dotnet/core/install/windows) and [Windows lifecycle information](https://learn.microsoft.com/windows/release-health/release-information).
- PhoneBox desktop agent running locally (default `http://127.0.0.1:8765`).


## Install a release

1. Download `PhoneBoxKeyboardAddon-win-x64.zip` from GitHub Releases.
2. Extract it.
3. Open PowerShell in the extracted folder and run:

   ```powershell
   .\install.ps1 -LaunchSettings
   ```

4. Test the PhoneBox connection, configure shortcuts, and save.
5. Enable **Start with Windows** in the settings panel or install with `-StartWithWindows` for silent startup.

If PowerShell execution policy blocks the script, use a temporary process-scoped bypass only:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\install.ps1 -LaunchSettings
```

The temporary policy ends when that PowerShell window closes. Installation is per-user and does not require administrator privileges.


## Optional Windows tray icon

In the HTML panel's **Preferences** section, enable **Show tray icon** and save to display an icon next to the clock without restarting the add-on. The custom PhoneBox Keyboard icon is used by the executable and tray. The context menu opens settings, toggles global shortcuts, and exits. It is disabled by default. If hidden, open the panel from the **Settings** Start Menu shortcut.

## Customize HTML and CSS

The interface source files are:

- HTML: `src/PhoneBoxKeyboardAddon/Web/settings.html`
- CSS: `src/PhoneBoxKeyboardAddon/Web/settings.css`

The stylesheet is separate for easier layout editing. Both files are embedded in the executable; after editing, rerun `scripts\publish-windows.ps1` and update the installation to apply your changes. The running app serves the stylesheet at the local `/settings.css` route.

## Settings and local data

The shortcut process remains hidden. The HTML settings interface opens only when the user selects **Settings** from the Start Menu or launches the executable with `--settings`.

| Data | Location |
|---|---|
| Settings | `%APPDATA%\PhoneBoxKeyboardAddon\settings.json` |
| Logs | `%LOCALAPPDATA%\PhoneBoxKeyboardAddon\Logs\addon.log` |
| Application | `%LOCALAPPDATA%\Programs\PhoneBoxKeyboardAddon` |
| Startup shortcut | User Startup folder, when enabled |

Settings remain outside the installation directory and are migrated between versions. Earlier schemas are migrated to v3 while preserving existing page 1–7 shortcuts; `ShowTrayIcon` defaults to false to preserve silent operation.

## Security and privacy

- The local settings server binds only to `127.0.0.1`, normally port `47831` with fallback through `47851`; it is not exposed to the LAN.
- The server validates `Host` and `Origin`, limits request sizes, and sets a Content Security Policy.
- PhoneBox URL validation requires local HTTP loopback. The client disables proxies and redirects and calls only fixed endpoints: `/api/config`, `/api/push`, and `/api/goto/{page}`.
- No account, telemetry, or remote-control service is used. The only external connection is the optional GitHub Releases check/download for the add-on itself. The add-on does not request elevation.
- The local settings server does not use an authentication token: `Host`/`Origin` checks are browser-origin protections, not authentication against other processes already running under the same Windows account. Do not run untrusted local software alongside the add-on.
- During shortcut capture, global registrations are temporarily suspended so the browser can receive key events. A 60-second lease restores them if the page closes unexpectedly.
- Releases are not Authenticode-signed in this version; SmartScreen may warn about downloaded executables. Verify the source, release and SHA-256 before running.

## Known limitations

- Windows 11 x64 on .NET 10-supported releases; Windows 10 only on Enterprise/LTSC editions and releases that remain within Microsoft's support lifecycle and are compatible with .NET 10.
- The PhoneBox agent must be running and reachable at the configured address.
- Next/Previous requires PhoneBox 0.9.31 or later because it uses the native relative-navigation webhooks.
- Windows can refuse global hotkeys already registered by another application; choose another combination in Settings.
- The executable is not Authenticode-signed yet.
- Global key capture, USB navigation, Startup integration, and self-contained publishing still require acceptance testing on a real Windows machine.

## Development

See `docs/ARCHITECTURE.md`, `docs/TEST-PLAN.md`, `docs/PUBLISHING.md`, `CONTRIBUTING.md`, and `SECURITY.md`. Repository publishing and maintainer instructions are kept in `docs/PUBLISHING.md`. When reporting a bug, include the add-on version, PhoneBox version, Windows version, key combinations and relevant log excerpt (remove private data).

## License and affiliation

MIT. This is an independent community add-on, not an official PhoneBox component and not affiliated with the PhoneBox maintainers.

## Uninstalling from the settings panel

The settings panel can uninstall the add-on without running PowerShell scripts. It schedules a hidden cleanup helper that waits for the app process to exit, then removes the installed executable, Start Menu shortcuts and Startup shortcut. User settings and logs are preserved by default. They are deleted only when the user explicitly checks **Also delete settings and logs** and confirms. The executable and optional tray icon use `src/PhoneBoxKeyboardAddon/Web/phonebox-keyboard.ico`. The status card now displays authoritative registration totals supplied by the local agent rather than relying only on JavaScript property counting.

## HTML settings page icon

The local settings page also uses the add-on icon as its browser favicon. The local server exposes the embedded assets at `/favicon.ico` and `/phonebox-keyboard.png`; the same PNG is used as the brand mark beside the page title. To replace the artwork, overwrite `src/PhoneBoxKeyboardAddon/Web/phonebox-keyboard.ico` and `phonebox-keyboard.png` while keeping the file names, then republish the executable.
