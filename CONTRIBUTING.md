# Contributing

Thanks for helping improve PhoneBox Keyboard Add-on.

## Before opening an issue

Include:
- Windows version and architecture.
- PhoneBox agent version.
- Add-on version.
- The shortcut combination being tested.
- Relevant sanitized lines from `%LOCALAPPDATA%\PhoneBoxKeyboardAddon\Logs\addon.log`.

Do not attach tokens, private device identifiers, or unrelated personal data.

## Development setup

1. Install the .NET 10 SDK on Windows.
2. Clone the repository.
3. Run `scripts/publish-windows.ps1`.
4. Run the produced executable with `--self-test` to exercise the built-in checks.
5. Open settings with `PhoneBoxKeyboardAddon.exe --settings`.
6. Follow `docs/TEST-PLAN.md` for the Windows acceptance checks.

## Pull requests

- Keep the add-on independent from the PhoneBox installation and source tree.
- Do not add external network services or telemetry.
- Keep the local settings server bound to loopback and preserve its Host/Origin checks.
- Add or extend self-tests for key mapping, settings validation, and serialization.
- Update README and CHANGELOG when behavior changes.
