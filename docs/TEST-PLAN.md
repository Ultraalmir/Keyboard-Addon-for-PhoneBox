# Test plan

Run the automated checks on a .NET 10-supported Windows 11 x64 release, or on a Windows 10 Enterprise/LTSC release that remains within Microsoft's support lifecycle and is supported by .NET 10:

```powershell
.\scripts\publish-windows.ps1
```

The publish script restores, builds, runs the built-in self-tests, and produces a self-contained `win-x64` executable.

## Automated checks

The built-in self-test verifies:

- Ten direct page slots and Windows virtual-key mappings, including Numpad 0 for page 10.
- Migration of legacy v1 settings and schema v2 settings into schema v3, preserving custom page shortcuts and defaulting the newer tray-icon preference to off when it is absent.
- Hotkey registration, suspension, and re-registration through the owning window thread.
- Default Next/Previous Numpad + and Numpad - mappings.
- Modifier validation and duplicate detection across all direct and relative actions.
- Relative navigation uses the official PhoneBox 0.9.31 `/api/goto/next` and `/api/goto/prev` webhooks without a local cursor.
- Native PhoneBox relative-navigation endpoint paths and SHA-256 checksum parsing for the update package.
- Embedded update-helper resource presence.
- Loopback URL validation and settings JSON round-trip.
- Dispatch to the HWND-owning thread and rejection of off-thread hotkey manager mutation.

## Manual Windows acceptance checklist

1. Install with `powershell -ExecutionPolicy Bypass -File .\\scripts\\install.ps1`.
2. Run `Abrir-Configuracoes.bat` from the extracted package folder.
3. Confirm the PhoneBox connection test succeeds while PhoneBox 0.9.31 or later is running.
4. With at least ten enabled pages, configure and test all direct slots 1–10. Confirm Numpad 0 can be used for page 10.
5. Test Next/Previous on a panel with more than ten enabled pages.
6. Confirm Next stops at the last page and Previous stops at the first page (no wraparound).
7. Navigate to a different page by touch/swipe, then use Next/Previous and confirm PhoneBox advances from the page actually displayed.
8. Trigger a direct-page shortcut and then Next/Previous; confirm PhoneBox follows its current page state.
9. Simulate a PhoneBox connection failure while pressing Next/Previous; verify the add-on reports the request failure.
10. Change each shortcut with **Capturar**, including a combination already registered by the add-on. Confirm the add-on suspends its hotkeys while listening, accepts the new combination, and restores hotkeys after capture and save. Repeat Save and Capture several times.
11. Cancel capture and confirm all previous hotkeys are restored. Close the settings tab while capture is active and confirm the 60-second lease restores hotkeys automatically.
12. Disable one direct slot, Next, and Previous individually; confirm each stops firing. Re-enable them and confirm they work.
13. Disable all shortcuts and confirm every action is inactive; re-enable them.
14. Configure a duplicate combination between a direct page and Next/Previous; confirm the settings API rejects it with a clear validation message.
15. Reserve a conflicting hotkey in another application and confirm the UI reports that Windows could not register it.
16. Enable startup, sign out/in or restart Windows, and confirm the add-on starts without a console, main window, or tray icon.
17. Open Settings using `Abrir-Configuracoes.bat` while the background instance is running and confirm a second process is not created.
18. Publish a test release, open the settings panel, confirm the update notice and version comparison, then run the update. Confirm the checksum is validated, only the installed executable is replaced, settings/logs/shortcuts remain intact, and the add-on restarts.
19. Simulate a bad package/checksum and confirm installation is refused without changing the installed executable. Simulate a helper replacement/startup failure and confirm the previous executable is restored.
20. Run `scripts\\uninstall.ps1`; confirm the app and shortcuts are removed while settings/logs remain unless `-RemoveSettings` is specified.

## Next/Previous navigation

PhoneBox 0.9.31 or later is required. The add-on delegates relative navigation to the official `POST /api/goto/next` and `/api/goto/prev` webhooks; PhoneBox handles the current-page state and stops at the first/last page.

## Environment limitations

Global hotkey capture, Windows startup integration, USB-connected PhoneBox navigation, and self-contained Windows publishing must be validated on a Windows machine. GitHub Actions runs the Windows build and built-in checks; hardware and user-session acceptance still requires local testing.


## Testes adicionais da versão 1.2.0

1. Com `ShowTrayIcon=false`, iniciar o complemento em segundo plano e confirmar que nenhum ícone do complemento aparece na área de notificação.
2. Abrir **Configurações** pelo arquivo `Abrir-Configuracoes.bat`, habilitar **Mostrar ícone na bandeja** e salvar. Confirmar que o ícone aparece sem reiniciar o processo.
3. Abrir o menu do ícone: confirmar **Abrir configurações**, alternância de atalhos globais e **Sair**.
4. Desabilitar os atalhos pelo menu da bandeja; confirmar no painel e por hotkey que foram desregistrados. Habilitá-los novamente.
5. Desabilitar o ícone pelo painel e salvar; confirmar que desaparece imediatamente, mas o processo e as hotkeys continuam funcionando.
6. Reiniciar o complemento com a opção ligada e confirmar que o ícone volta; repetir com a opção desligada e confirmar ausência.
7. Modificar `Web/settings.css`, publicar novamente e confirmar que a alteração visual aparece em `/settings.css`.


### Status summary and uninstall

- Confirm the `/api/status` response includes `registeredCount`, `enabledRegistrationCount` and `actionCount`, and that the panel displays those server-provided totals rather than inferring the totals from JSON property enumeration.

- Confirm the status card reports 12 configurable actions and a non-zero enabled count when default hotkeys are enabled.
- Disable all shortcuts and confirm the registered count becomes zero while enabled/configurable totals remain accurate.
- Enable the tray icon and confirm the custom PhoneBox Keyboard icon appears; disable it and confirm the icon disappears.
- Open the settings panel from the installed executable and run the in-panel uninstall with the data-preservation checkbox unchecked; confirm the program and shortcuts are removed while settings/logs remain.
- Reinstall, then repeat with the data-removal checkbox checked and confirm settings/logs are removed only after explicit confirmation.
- Run the panel from a development/publish folder and confirm in-panel uninstall is unavailable.


## Versão 1.3.0 — status, ícone e desinstalação

1. Confirmar que `/api/status` fornece `registeredCount`, `enabledRegistrationCount` e `actionCount`. Com os padrões, a interface deve mostrar 9 ações habilitadas de 12 ações configuráveis; o total efetivamente registrado depende de conflitos de hotkey no Windows.
2. Desabilitar globalmente os atalhos e confirmar que o contador de registrados passa a zero, enquanto os totais de ações habilitadas/configuráveis continuam corretos.
3. Habilitar o ícone da bandeja e verificar o ícone personalizado em 16/32 px, menu de contexto e duplo clique; desabilitar e confirmar que ele desaparece.
4. Abrir o painel pela instalação padrão usando `Abrir-Configuracoes.bat`. Confirmar que a opção de desinstalar fica habilitada.
5. Desinstalar sem marcar a remoção de dados: confirmar que o executável, atalhos antigos do Menu Iniciar e Startup são removidos, enquanto `settings.json` e logs permanecem.
6. Reinstalar, marcar a remoção de dados, confirmar duas vezes e verificar que configurações/logs também são removidos.
7. Executar uma cópia de desenvolvimento/publicação fora da pasta instalada e confirmar que o painel não oferece desinstalação dessa cópia.

## Bilingual UI regression

- Switch from PT to EN and confirm all panel labels, generated shortcut rows, status text, capture dialog, confirmations, and toast messages are English.
- Switch back to PT and confirm Brazilian Portuguese is restored.
- Restart the add-on and confirm the last selected language is retained.
- Upgrade from an earlier settings file without `uiLanguage`; confirm it defaults to `pt-BR` without resetting shortcuts.
