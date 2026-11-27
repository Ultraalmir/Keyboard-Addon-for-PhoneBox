# Changelog

## [Unreleased]

## [1.5.1] - 2026-11-26

### Documentation
- Clarify that upgrades from 1.4.0 require one manual installation of 1.5.0 before in-app updates are available.
- Align version references and maintainer instructions with the 1.5.1 release candidate.
- Document the status-card counters separately from tray-icon behavior.

## [1.5.0] - 2026-11-26

### Added
- Added a GitHub Releases update checker to the local settings panel, with installed/latest version details and a direct release link.
- Added an assisted in-app updater that downloads the official release package, verifies its SHA-256 checksum, backs up the installed executable, replaces only that executable, and restarts the add-on.
- Added rollback to the previous executable if the update helper encounters a replacement failure.

### Changed
- Switched Next/Previous to PhoneBox 0.9.31 native `POST /api/goto/next` and `POST /api/goto/prev` webhooks.
- Removed the add-on-maintained relative navigation cursor; PhoneBox now resolves the currently displayed page and stops at the first/last page.
- Kept user settings and logs outside the installation directory so incremental executable updates preserve them.
- Bumped the application version to 1.5.0.

### Compatibility
- PhoneBox 0.9.31 or later is required for native Next/Previous navigation. Direct page shortcuts continue using the existing `/api/config` and `/api/goto/{page}` endpoints.

### Documentation
- Clarify .NET 10 Windows compatibility in both user guides and the project website; Windows 10 is limited to Enterprise/LTSC releases that remain within Microsoft's support lifecycle and are supported by .NET 10.
- Move maintainer-only build, self-test, packaging, Git and release commands from the user-facing READMEs into `docs/PUBLISHING.md`.
- Correct the test plan to describe migration from legacy schema v1 and schema v2 settings into schema v3.

### Accessibility
- Localize project website ARIA labels with the PT-BR / English language selector.

## [1.4.0] - 2026-11-25

### Added
- Added a PT-BR / EN language selector to the settings panel.
- Persisted the selected UI language in the existing user settings file, defaulting older installs to Brazilian Portuguese.
- Translated static and dynamic UI content, including shortcut rows, capture flow, status, connection feedback, confirmations, and uninstall messages.
- Added accessibility labels and keyboard focus styling to the language selector.

### Changed
- Kept Brazilian Portuguese as the default language for new and existing installations.
- Bumped the application version to 1.4.0.

## [1.3.2] - 2026-11-26

### Added
- Added the embedded app icon as the settings page favicon (`/favicon.ico`) and browser/touch PNG icon (`/phonebox-keyboard.png`).
- Replaced the generic inline header mark with the PhoneBox Keyboard brand artwork.

### Fixed
- Added local static-asset routes so browsers can load the embedded icons from the self-contained executable without external files or network access.
- Added no-store caching for icon assets so updated artwork appears immediately after upgrading.

## [1.3.1] - 2026-11-25

### Fixed
- Fix the uninstall scheduler compile error by storing and disposing the started cleanup process instead of using a null-coalescing expression as a standalone statement.

## [1.3.0] - 2026-11-25

### Added
- Added a custom PhoneBox Keyboard icon for the Windows executable and optional notification-area icon.
- Added an in-panel uninstall action with a clear choice to preserve or permanently remove user settings and logs.
- Added a hidden cleanup helper that waits for the app to exit before deleting the installed executable, shortcuts and optional user data; no PowerShell execution-policy change is needed for in-app uninstall.

### Fixed
- Fixed the registered-shortcut status counter by publishing explicit lower-camel-case registration fields and authoritative server-side totals.
- Added uninstall availability detection so development builds do not offer to remove an installation they are not running from.

### Notes
- Uninstall preserves user settings and logs by default. User data is removed only after an explicit confirmation and checkbox selection.
- The custom app icon is embedded in the executable and used by the Windows tray icon.


## [1.2.0] - 2026-11-25

### Added
- Added an optional Windows notification-area (system tray) icon, controlled from the HTML settings panel and hidden by default.
- Added a tray context menu to open settings, toggle global shortcuts, and exit the add-on.
- Extracted the settings page stylesheet into `src/PhoneBoxKeyboardAddon/Web/settings.css` for easier layout customization.
- Added a dedicated local `/settings.css` route with no-store caching.

### Changed
- Bumped settings schema to version 3; existing users keep their prior shortcut/startup settings and tray visibility defaults to off.
- Updated status and settings UI copy to reflect the selected tray-icon mode.

### Notes
- The tray icon uses the custom PhoneBox Keyboard artwork. The tray menu is only available while the icon preference is enabled.
- The HTML and CSS are embedded in the published executable. Edit the source files and republish to apply layout changes.


## [1.1.2] - 2026-11-25

### Fixed
- Fix a compile-time `char` to `string` conversion in the display label for top-row numeric keys (`Top0`–`Top9`).
- Add self-test coverage for display labels of top-row and Numpad digit keys to prevent regression.


## [1.1.1] - 2026-11-25

### Fixed
- Send stable PhoneBox page IDs to `/api/goto` instead of relying on numeric indexes, reducing ambiguity when disabled pages exist.
- Extend the Windows self-test to exercise actual hotkey registration, suspension, and re-registration through the HWND-owning thread.
- Add a regression test for migrating the original seven-shortcut settings into ten page slots and the two relative actions.

## [1.1.0] - 2026-11-25

### Added
- Expanded direct page shortcuts from 7 to 10 configurable slots.
- Added separately configurable **Next page** and **Previous page** hotkeys.
- Added live page-list discovery through the PhoneBox public `/api/config` endpoint; navigation sends stable page IDs through `/api/goto`, while relative navigation wraps across all enabled pages, including pages beyond slot 10.
- Added a configurable navigation cursor/start position so relative navigation can be synchronized after manual navigation on the phone.
- Added tests for ten page slots, relative cursor wrapping, and collisions between direct and relative shortcuts.

### Changed
- Migrated settings schema from version 1 to version 2 while preserving existing page 1–7 bindings and adding disabled page slots 8–10.
- Default relative shortcuts are `Ctrl + Numpad +` and `Ctrl + Numpad -`.
- Page-list information is cached briefly to avoid fetching the full PhoneBox configuration on every key press.

### Fixed
- Retained the v1.0.1 thread-affinity fix: all `RegisterHotKey` and `UnregisterHotKey` calls are marshalled to the hidden window's owning thread.
- Extended validation and duplicate-hotkey detection across all 10 direct slots and both relative navigation actions.
- Commit the local Next/Previous cursor only after PhoneBox accepts the navigation request, so a failed request does not desynchronize the add-on further.
- Persist normalized/migrated settings after loading, including repaired slot lists and missing navigation bindings.
- Expanded self-tests for more-than-ten-page wraparound, all twelve active actions, null settings entries, and the owner-thread dispatcher.

### Important behavior note
- The PhoneBox public API exposes page navigation and page configuration, but does not report which page is currently visible on the phone. The add-on therefore tracks a local navigation cursor. Direct page hotkeys update it; if the user changes pages by touch, swipe, PhoneBox rules, or another controller, they should update the configured cursor/start position in the add-on panel before using Next/Previous.

## [1.0.1] - 2026-11-25

### Fixed
- Marshal hotkey registration and removal to the message-only window's owning UI thread.
- Add a thread-affinity guard so future worker-thread calls fail clearly instead of corrupting registered hotkey state.
- Serialize capture lease transitions and restore hotkeys through the same dispatcher.
- Prevent the PhoneBox HTTP client from following redirects.

### Changed
- Remove the redundant NumLock preference; Windows virtual-key mapping determines when Numpad keys produce `VK_NUMPADn`.
- Handle the `--background` startup argument explicitly.
- Expand self-tests to verify that background requests execute on the HWND-owning thread and direct worker-thread mutation is rejected.

## [1.0.0] - 2026-11-25

- First structured release of the independent Windows add-on.
- Added global, user-configurable keyboard shortcuts for PhoneBox page navigation.
- Added an embedded local HTML settings panel.
- Added global and per-shortcut enable/disable controls.
- Added silent background startup and per-user Startup-folder management.
- Added single-instance guard, local-only HTTP settings server, status endpoint, diagnostics and rotating logs.
- Added self-contained win-x64 publishing, install/uninstall scripts and GitHub Actions CI/release workflows.
