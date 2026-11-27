using System.Diagnostics;
using System.Windows.Forms;

namespace PhoneBoxKeyboardAddon;

internal sealed class AddonApplicationContext : ApplicationContext
{
    private readonly ConfigStore _store;
    private readonly PhoneBoxClient _phoneBox;
    private readonly HotkeyManager _hotkeys;
    private readonly HotkeyWindow _window;
    private readonly LocalSettingsServer _server;
    private readonly TrayIconController _trayIcon;
    private readonly System.Windows.Forms.Timer _updateResultTimer;

    internal AddonApplicationContext(bool openSettings)
    {
        _store = new ConfigStore();
        var settings = _store.Load();
        _phoneBox = new PhoneBoxClient();
        _hotkeys = new HotkeyManager(_phoneBox, settings);
        _window = new HotkeyWindow(_hotkeys, ExitThread);
        _hotkeys.AttachWindow(_window.Handle);
        _hotkeys.Apply(settings);

        _trayIcon = new TrayIconController(Program.OpenSettingsPage, ToggleShortcutsFromTray, ExitThread, settings.ShortcutsEnabled);
        _trayIcon.SetVisible(settings.ShowTrayIcon);
        _server = new LocalSettingsServer(_store, _hotkeys, _phoneBox, _window, _trayIcon);
        _server.Start();

        _updateResultTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _updateResultTimer.Tick += CheckUpdateResult;
        _updateResultTimer.Start();

        if (settings.StartWithWindows)
        {
            try { StartupManager.SetEnabled(true); }
            catch (Exception ex) { AddonLog.Error("Não foi possível sincronizar o atalho de inicialização.", ex); }
        }

        if (openSettings)
        {
            try { Process.Start(new ProcessStartInfo(_server.SettingsUrl) { UseShellExecute = true }); }
            catch (Exception ex) { AddonLog.Error("Não foi possível abrir as configurações.", ex); }
        }
    }

    private void CheckUpdateResult(object? sender, EventArgs e)
    {
        if (!File.Exists(AddonPaths.UpdateResultFile)) return;

        try
        {
            var lines = File.ReadAllLines(AddonPaths.UpdateResultFile);
            if (lines.Length < 2 || string.IsNullOrWhiteSpace(lines[1])) return;
            File.Delete(AddonPaths.UpdateResultFile);

            var succeeded = lines[0].Equals("success", StringComparison.OrdinalIgnoreCase);
            var version = lines[1].Trim();
            var title = succeeded ? "PhoneBox atualizado" : "Falha na atualização do PhoneBox";
            var message = succeeded
                ? $"O PhoneBox Keyboard Add-on foi atualizado para a versão {version}."
                : "A atualização não foi concluída. O aplicativo tentou recuperar a versão anterior. Consulte o log para detalhes.";

            AddonLog.Info(succeeded
                ? $"Notificação de atualização concluída exibida para a versão {version}."
                : "Notificação de falha na atualização exibida.");
            _trayIcon.ShowNotification(title, message, succeeded ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
        catch (IOException)
        {
            // The updater may still be writing the result marker; retry on the next tick.
        }
        catch (Exception ex)
        {
            AddonLog.Error("Não foi possível processar o resultado da atualização.", ex);
        }
    }

    private void ToggleShortcutsFromTray()
    {
        try
        {
            var settings = _store.Load();
            settings.ShortcutsEnabled = !settings.ShortcutsEnabled;
            _store.Save(settings);
            _hotkeys.Apply(settings); // Tray menu events run on the WinForms owner thread.
            _trayIcon.SetShortcutsEnabled(settings.ShortcutsEnabled);
            AddonLog.Info(settings.ShortcutsEnabled ? "Atalhos globais ativados pelo menu da bandeja." : "Atalhos globais desativados pelo menu da bandeja.");
        }
        catch (Exception ex)
        {
            AddonLog.Error("Não foi possível alterar os atalhos pelo menu da bandeja.", ex);
        }
    }

    protected override void ExitThreadCore()
    {
        _updateResultTimer.Stop();
        _updateResultTimer.Dispose();
        _trayIcon.Dispose();
        _server.Dispose();
        _hotkeys.Dispose();
        _phoneBox.Dispose();
        _window.Dispose();
        base.ExitThreadCore();
    }
}
