# Architecture

PhoneBox Keyboard Add-on is an independent Windows companion. It does not patch, inject into, or replace PhoneBox files.

## Runtime components

- **WinForms message pump:** keeps a hidden message-only window alive. The window has no taskbar presence, main UI, or tray icon.
- **Global shortcut manager:** uses Windows `RegisterHotKey` and `WM_HOTKEY`. The hidden HWND owns every registration. HTTP worker requests are marshalled to that HWND's thread using a private window message and a concurrent work queue.
- **PhoneBox HTTP client:** sends `POST /api/goto/{page}?countdown=0` to the local PhoneBox agent. The default endpoint is `http://127.0.0.1:8765`.
- **Page discovery:** reads `GET /api/config`, then extracts enabled pages in PhoneBox order. This endpoint is used to validate direct page positions and to calculate Next/Previous targets. The page list is cached for up to 10 seconds and can be force-refreshed from the settings page. This version requests the global page list (no `X-PhoneBox-Device` header); per-phone page-profile selection is not implemented.
- **Relative navigation:** on PhoneBox 0.9.31 or later, Next/Previous sends the official `POST /api/goto/next` and `POST /api/goto/prev` webhooks. The PhoneBox agent resolves the currently displayed page and stops at the first/last page; the add-on no longer keeps a local navigation cursor. Direct page requests use stable PhoneBox page IDs instead of numeric URL segments.
- **Update manager:** checks the latest stable GitHub Release, downloads the release ZIP and SHA256SUMS file, validates the package hash and expected executable entry, then starts an embedded PowerShell helper. The helper waits for the installed process to exit, stages and replaces only the executable, keeps a backup until the new process starts, and restores the previous executable if replacement/startup fails.
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

## Next/Previous navigation

PhoneBox 0.9.31 owns relative navigation. The add-on does not calculate a destination or maintain a local cursor. It sends `POST /api/goto/next` or `POST /api/goto/prev`; PhoneBox resolves the page currently displayed on the connected phone and stops at the first/last page. This avoids stale state when the user navigates manually or through PhoneBox rules.

## Data flow

1. Windows detects a modifier + key combination registered through `RegisterHotKey`.
2. Windows delivers `WM_HOTKEY` to the add-on's hidden message-only window.
3. Direct actions read the enabled page list (10-second cache), select a fixed one-based position, and send `POST /api/goto/{page}?countdown=0` using the stable page ID.
4. Next/Previous sends the official `POST /api/goto/next` or `POST /api/goto/prev` webhook.
5. PhoneBox resolves the currently displayed page and emits the navigation event to the connected device.

## Security boundaries

- The settings server binds to `IPAddress.Loopback`, never to `IPAddress.Any`.
- The local settings API accepts requests only with the expected `Host`; requests with an `Origin` must match the local origin.
- The PhoneBox destination is restricted to loopback HTTP. The HTTP client disables proxy use and redirects and calls only fixed endpoints.
- No cloud account, telemetry, or remote command endpoint is added. The only external requests are the optional GitHub Releases metadata/package/checksum requests used by the update feature. The local settings API has no bearer token and is intended for the signed-in user's local processes; Host/Origin validation is a browser-origin defense, not authentication against other software already running under the same Windows account.
- The process runs as the signed-in user and does not request elevation.

## Update compatibility

The add-on is installed separately under `%LOCALAPPDATA%\Programs\PhoneBoxKeyboardAddon`. Settings and logs remain outside the install directory and are schema-migrated independently, so updating PhoneBox or the add-on does not require patching PhoneBox files. Direct page shortcuts depend on PhoneBox exposing `/api/config` and `/api/goto/{page}`; relative navigation requires PhoneBox 0.9.31 or later and its `/api/goto/next` and `/api/goto/prev` webhooks.


## Ícone personalizado e desinstalação (v1.3.0)

`TrayIconController` é criado pelo `AddonApplicationContext`. A preferência `ShowTrayIcon` é persistida em `settings.json`; o padrão é `false`. A interface HTML envia a preferência pelo endpoint local `/api/config`. O servidor despacha a aplicação da preferência para a thread proprietária da janela WinForms, e só então alterna `NotifyIcon.Visible`. O menu da bandeja permite abrir o painel, ativar/desativar atalhos globais e encerrar o complemento. Quando a preferência está desligada, o processo permanece oculto e o painel continua acessível pelo atalho **Configurações** do Menu Iniciar.

## Assets do painel

A interface fonte está em `src/PhoneBoxKeyboardAddon/Web/settings.html` e o CSS separado em `src/PhoneBoxKeyboardAddon/Web/settings.css`. Ambos são recursos incorporados ao executável e servidos exclusivamente pelo servidor loopback (`/` e `/settings.css`). Portanto, alterações visuais no repositório passam a valer depois de compilar/publicar novamente o executável.


The status endpoint includes explicit `registeredCount`, `enabledRegistrationCount` and `actionCount` fields. The settings page uses these authoritative totals instead of relying on client-side enumeration of registration-record property names.
