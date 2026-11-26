using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace PhoneBoxKeyboardAddon;

/// <summary>Optional Windows notification-area icon and its context menu.</summary>
internal sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _appIcon;
    private readonly ToolStripMenuItem _shortcutsItem;
    private readonly Action _openSettings;
    private readonly Action _toggleShortcuts;
    private readonly Action _exit;
    private bool _disposed;

    internal TrayIconController(Action openSettings, Action toggleShortcuts, Action exit, bool shortcutsEnabled)
    {
        _openSettings = openSettings;
        _toggleShortcuts = toggleShortcuts;
        _exit = exit;

        var menu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Abrir configurações");
        openItem.Click += (_, _) => _openSettings();

        _shortcutsItem = new ToolStripMenuItem("Atalhos globais ativados")
        {
            CheckOnClick = false,
            Checked = shortcutsEnabled
        };
        _shortcutsItem.Click += (_, _) => _toggleShortcuts();

        var exitItem = new ToolStripMenuItem("Sair");
        exitItem.Click += (_, _) => _exit();

        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_shortcutsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _appIcon = LoadApplicationIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "PhoneBox Keyboard Add-on",
            ContextMenuStrip = menu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => _openSettings();
    }

    internal bool Visible => _notifyIcon.Visible;

    internal void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.Visible = visible;
    }

    internal void SetShortcutsEnabled(bool enabled)
    {
        if (_disposed) return;
        _shortcutsItem.Checked = enabled;
        _shortcutsItem.Text = enabled ? "Atalhos globais ativados" : "Atalhos globais desativados";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        var menu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.Dispose();
        menu?.Dispose();
        _appIcon.Dispose();
    }

    private static Icon LoadApplicationIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "PhoneBoxKeyboardAddon.Web.phonebox-keyboard.ico");
        return stream is null ? (Icon)SystemIcons.Application.Clone() : new Icon(stream);
    }
}
