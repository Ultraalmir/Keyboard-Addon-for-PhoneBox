# Architecture

PhoneBox Keyboard Add-on is an independent Windows companion. It does not patch, inject into, or replace PhoneBox files.

## Runtime components

- **WinForms message pump:** keeps a hidden message-only window alive. The window has no taskbar presence, main UI, or tray icon.
- **Global shortcut manager:** uses Windows `RegisterHotKey` and `WM_HOTKEY`. The hidden HWND owns every registration. HTTP worker requests are marshalled to that HWND's thread using a private window message and a concurrent work queue.
- **PhoneBox HTTP client:** sends `POST /api/goto/{page}?countdown=0` to the local PhoneBox agent. The default endpoint is `http://127.0.0.1:8765`.
- **Page discovery:** reads `GET /api/config`, then extracts enabled pages in PhoneBox order. This endpoint is used to validate direct page positions and to calculate Next/Previous targets. The page list is cached for up to 10 seconds and can be force-refreshed from the settings page. This version requests the global page list (no `X-PhoneBox-Device` header); per-phone page-profile selection is not implemented.
- **Relative navigation cursor:** stores the last page position requested by this add-on. Next/Previous advances or retreats within the currently enabled page list, wrapping at either end. The cursor is initialized from `NavigationStartPage` and updated by direct/relative add-on shortcuts. Navigation requests use stable PhoneBox page IDs instead of numeric URL segments.
- **Local settings server:** serves the embedded HTML page and a small JSON API on IPv4 loopback only. The listener tries ports 47831 through 47851. Host and Origin checks reject requests addressed through other hosts or origins. During shortcut capture, the server temporarily unregisters global shortcuts so the browser can receive key events; a 60-second server-side lease restores them if the settings page closes unexpectedly.
- **Configuration store:** persists JSON under `%APPDATA%\PhoneBoxKeyboardAddon`. Older schemas are normalized/migrated while preserving existing page 1–7 bindings, adding slots 8–10 and Next/Previous, and keeping tray visibility off by default. Logs are stored under `%LOCALAPPDATA%\PhoneBoxKeyboardAddon\Logs`.
- **Startup integration:** an optional per-user `.lnk` in the Windows Startup folder starts the hidden executable. No administrator rights or Run registry key is needed.
- **Single-instance guard:** a session-local named mutex prevents duplicate background processes. A second `--settings` invocation opens the existing local settings page.
- **Optional tray icon:** `TrayIconController` uses the custom multi-size `Web/phonebox-keyboard.ico` asset for the Windows executable and notification area. Visibility is controlled by `ShowTrayIcon` without restarting the add-on.
- **In-panel uninstaller:** the local API validates that the running executable is the per-user installed binary, then starts a hidden CMD cleanup helper. The helper waits for the add-on process to exit before deleting the install directory and shortcuts. Settings and logs are preserved unless the user explicitly chooses to remove them.

## Hotkey model

The add-on supports up to 12 actions:

- Ten direct page slots (positions 1–10), each independently configurable and activatable.
- One configurable Next page action.
- One configurable Previous page action.

The default direct bindings preserve the previous seven shortcuts: `Ctrl + Numpad 1` through `Ctrl + Numpad 7`. Slots 8–10 start disabled (`Ctrl + Numpad 8`, `Ctrl + Numpad 9`, `Ctrl + Numpad 0`). Relative navigation starts enabled with `Ctrl + Numpad +` and `Ctrl + Numpad -`.

All active combinations are checked for duplicates before saving. Windows reports conflicts when another application already owns a combination. At least one modifier (Ctrl, Alt, Shift, or Win) is required.

## Next/Previous limitation and synchronization

The PhoneBox public API exposes `POST /api/goto/{page}` and the public page configuration, but not the page currently visible on the phone. The add-on cannot reliably infer touch/swipe navigation, rules, volume-key navigation, or page changes made by another controller.

For this reason, Next/Previous uses an add-on-maintained cursor:

1. At startup, the cursor uses `NavigationStartPage` (default: 1).
2. A direct page shortcut moves the cursor to that page.
3. Next/Previous moves one position in the enabled PhoneBox page list and wraps at the beginning/end.
4. After changing the PhoneBox page outside this add-on, set **Navigation start/current position** in the settings page to the page currently visible and save. This resynchronizes the cursor.

The add-on detects the page count and order dynamically, so relative navigation can reach more than ten pages even though there are only ten direct shortcut slots.

## Data flow

1. Windows detects a modifier + key combination registered through `RegisterHotKey`.
2. Windows delivers `WM_HOTKEY` to the add-on's hidden message-only window.
3. The add-on loads the current settings and reads the enabled page list (10-second cache).
4. Direct actions select a fixed one-based page position; Next/Previous updates the local cursor with wraparound.
5. The HTTP client sends `POST /api/goto/{page}?countdown=0` to PhoneBox.
6. PhoneBox emits its existing `goto` event to the connected device.

## Security boundaries

- The settings server binds to `IPAddress.Loopback`, never to `IPAddress.Any`.
- The local settings API accepts requests only with the expected `Host`; requests with an `Origin` must match the local origin.
- The PhoneBox destination is restricted to loopback HTTP. The HTTP client disables proxy use and redirects and calls only fixed endpoints.
- No cloud account, telemetry, or remote command endpoint is added. The local settings API has no bearer token and is intended for the signed-in user's local processes; Host/Origin validation is a browser-origin defense, not authentication against other software already running under the same Windows account.
- The process runs as the signed-in user and does not request elevation.

## Update compatibility

The add-on is installed separately under `%LOCALAPPDATA%\Programs\PhoneBoxKeyboardAddon`. Settings and logs remain outside the install directory and are schema-migrated independently, so updating PhoneBox or the add-on does not require patching PhoneBox files. Relative navigation depends on PhoneBox continuing to expose its documented `/api/config` and `/api/goto/{page}` endpoints.


## Ícone personalizado e desinstalação (v1.3.0)

`TrayIconController` é criado pelo `AddonApplicationContext`. A preferência `ShowTrayIcon` é persistida em `settings.json`; o padrão é `false`. A interface HTML envia a preferência pelo endpoint local `/api/config`. O servidor despacha a aplicação da preferência para a thread proprietária da janela WinForms, e só então alterna `NotifyIcon.Visible`. O menu da bandeja permite abrir o painel, ativar/desativar atalhos globais e encerrar o complemento. Quando a preferência está desligada, o processo permanece oculto e o painel continua acessível pelo atalho **Configurações** do Menu Iniciar.

## Assets do painel

A interface fonte está em `src/PhoneBoxKeyboardAddon/Web/settings.html` e o CSS separado em `src/PhoneBoxKeyboardAddon/Web/settings.css`. Ambos são recursos incorporados ao executável e servidos exclusivamente pelo servidor loopback (`/` e `/settings.css`). Portanto, alterações visuais no repositório passam a valer depois de compilar/publicar novamente o executável.


The status endpoint includes explicit `registeredCount`, `enabledRegistrationCount` and `actionCount` fields. The settings page uses these authoritative totals instead of relying on client-side enumeration of registration-record property names.
