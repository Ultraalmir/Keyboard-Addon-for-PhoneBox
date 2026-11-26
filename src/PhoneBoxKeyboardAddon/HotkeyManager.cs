using System.Runtime.InteropServices;

namespace PhoneBoxKeyboardAddon;

internal sealed class HotkeyManager : IDisposable
{
    private const uint ModNoRepeat = 0x4000;
    private const int FirstPageId = 6101; // 6101–6110: direct page slots 1–10.
    private const int NextPageId = 6111;
    private const int PreviousPageId = 6112;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private readonly PhoneBoxClient _client;
    private readonly Action<int>? _persistNavigationCursor;
    private readonly Dictionary<int, RegisteredHotkey> _registered = [];
    private List<HotkeyRegistrationStatus> _statuses = [];
    private IntPtr _windowHandle;
    private int _ownerThreadId;
    private AddonSettings _settings;
    private bool _suspended;
    private int _navigationCursorPage;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal HotkeyManager(PhoneBoxClient client, AddonSettings settings, Action<int>? persistNavigationCursor = null)
    {
        _client = client;
        _persistNavigationCursor = persistNavigationCursor;
        _settings = settings.DeepClone();
        _navigationCursorPage = Math.Max(1, _settings.NavigationStartPage);
    }

    internal void AttachWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("Handle da janela inválido.", nameof(handle));
        if (_windowHandle != IntPtr.Zero) throw new InvalidOperationException("A janela já foi associada ao gerenciador.");
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _windowHandle = handle;
    }

    internal IReadOnlyList<HotkeyRegistrationStatus> Status
    {
        get { lock (_sync) return _statuses.ToArray(); }
    }

    internal int NavigationCursorPage
    {
        get { lock (_sync) return _navigationCursorPage; }
    }

    internal IReadOnlyList<HotkeyRegistrationStatus> Apply(AddonSettings settings)
    {
        EnsureOwnerThread();
        lock (_sync)
        {
            _settings = settings.DeepClone();
            _navigationCursorPage = Math.Max(1, _settings.NavigationStartPage);
            return ApplyUnsafe();
        }
    }

    internal IReadOnlyList<HotkeyRegistrationStatus> SetSuspended(bool suspended)
    {
        EnsureOwnerThread();
        lock (_sync)
        {
            _suspended = suspended;
            return ApplyUnsafe();
        }
    }

    internal bool IsSuspended
    {
        get { lock (_sync) return _suspended; }
    }

    private void EnsureOwnerThread()
    {
        if (_ownerThreadId == 0 || Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("RegisterHotKey e UnregisterHotKey devem ser executados na thread proprietária da janela Win32.");
    }

    // All RegisterHotKey/UnregisterHotKey calls are made only from this HWND-owning thread.
    private IReadOnlyList<HotkeyRegistrationStatus> ApplyUnsafe()
    {
        UnregisterAllUnsafe();
        var result = new List<HotkeyRegistrationStatus>(12);

        foreach (var binding in _settings.Bindings.OrderBy(b => b.Page))
        {
            var action = new RegisteredHotkey(
                $"page-{binding.Page}", $"Página {binding.Page}", binding.Enabled,
                binding.Modifiers, binding.Key, binding.Page, NavigationDirection.None);
            RegisterOne(FirstPageId + binding.Page - 1, action, result);
        }

        RegisterOne(NextPageId, new RegisteredHotkey(
            "next", "Próxima página", _settings.NextPage.Enabled,
            _settings.NextPage.Modifiers, _settings.NextPage.Key, null, NavigationDirection.Next), result);
        RegisterOne(PreviousPageId, new RegisteredHotkey(
            "previous", "Página anterior", _settings.PreviousPage.Enabled,
            _settings.PreviousPage.Modifiers, _settings.PreviousPage.Key, null, NavigationDirection.Previous), result);

        _statuses = result;
        var registered = _registered.Count;
        var enabled = result.Count(r => r.Enabled);
        AddonLog.Info(_suspended
            ? "Atalhos suspensos temporariamente para captura do teclado."
            : $"Atalhos aplicados: {registered} de {enabled} habilitados registrados ({result.Count} ações configuráveis)." );
        return _statuses.ToArray();
    }

    private void RegisterOne(int id, RegisteredHotkey action, List<HotkeyRegistrationStatus> result)
    {
        var combination = HotkeyCatalog.DisplayCombination(action.Modifiers, action.Key);
        if (_suspended)
        {
            result.Add(new(action.ActionId, action.Label, false, combination, false,
                "Atalhos temporariamente suspensos durante a captura do teclado."));
            return;
        }

        if (!_settings.ShortcutsEnabled || !action.Enabled)
        {
            result.Add(new(action.ActionId, action.Label, false, combination, false,
                _settings.ShortcutsEnabled ? "Atalho desativado." : "Todos os atalhos estão desativados."));
            return;
        }

        if (!HotkeyCatalog.TryGetVirtualKey(action.Key, out var vk))
        {
            result.Add(new(action.ActionId, action.Label, true, combination, false, "Tecla não suportada."));
            return;
        }
        if (!HotkeyCatalog.TryGetModifiers(action.Modifiers, out var modifiers, out var modifierError))
        {
            result.Add(new(action.ActionId, action.Label, true, combination, false, modifierError ?? "Modificador inválido."));
            return;
        }

        if (_windowHandle == IntPtr.Zero || !RegisterHotKey(_windowHandle, id, modifiers | ModNoRepeat, vk))
        {
            var error = Marshal.GetLastWin32Error();
            var message = error == 1409
                ? "Esta combinação já está registrada por outro aplicativo. Escolha outra."
                : $"O Windows recusou o atalho (erro {error}).";
            result.Add(new(action.ActionId, action.Label, true, combination, false, message));
            AddonLog.Warn($"Falha ao registrar {action.Label}: {message}");
            return;
        }

        _registered[id] = action;
        result.Add(new(action.ActionId, action.Label, true, combination, true, "Atalho registrado."));
    }

    internal void HandleHotkey(int id)
    {
        EnsureOwnerThread();
        RegisteredHotkey? action;
        AddonSettings settings;
        lock (_sync)
        {
            if (_suspended || !_registered.TryGetValue(id, out action)) return;
            settings = _settings.DeepClone();
        }

        if (!settings.ShortcutsEnabled || !action.Enabled) return;
        _ = ExecuteNavigationAsync(settings, action);
    }

    private async Task ExecuteNavigationAsync(AddonSettings settings, RegisteredHotkey action)
    {
        await _navigationGate.WaitAsync();
        try
        {
            // PhoneBox does not expose the currently visible page through its local webhook API.
            // Fetch the enabled page order and use a user-configurable cursor for relative navigation.
            var pageResult = await _client.GetPagesAsync(settings);
            if (!pageResult.Success)
            {
                _client.RecordFailure(pageResult.Message);
                return;
            }

            var count = pageResult.Pages.Count;
            if (count == 0)
            {
                _client.RecordFailure("O PhoneBox não informou páginas habilitadas.");
                return;
            }

            int targetPage;
            string? validationError = null;
            lock (_sync)
            {
                if (action.Direction == NavigationDirection.None)
                {
                    targetPage = action.Page!.Value;
                    if (targetPage > count)
                        validationError = $"A página {targetPage} não existe entre as {count} páginas habilitadas no PhoneBox.";
                }
                else
                {
                    var current = NavigationCursor.Normalize(_navigationCursorPage, settings.NavigationStartPage, count);
                    targetPage = NavigationCursor.Move(current, count,
                        action.Direction == NavigationDirection.Next ? 1 : -1);
                }
            }

            if (validationError is not null)
            {
                _client.RecordFailure(validationError);
                return;
            }

            // Use the stable page ID returned by PhoneBox instead of a numeric path.
            // This avoids resolving the wrong page when disabled pages exist in the config.
            var target = pageResult.Pages.FirstOrDefault(p => p.Position == targetPage);
            if (target is null)
            {
                _client.RecordFailure($"Não foi possível identificar a página habilitada na posição {targetPage}.");
                return;
            }

            // Commit the local cursor only after PhoneBox accepted the navigation event.
            // A failed HTTP request must not silently move the cursor away from the phone.
            var navigation = await _client.NavigateAsync(settings, target.Id, target.Title);
            if (navigation.Success)
            {
                lock (_sync) _navigationCursorPage = targetPage;
                try { _persistNavigationCursor?.Invoke(targetPage); }
                catch (Exception persistException)
                {
                    AddonLog.Error("A página foi aberta, mas não foi possível persistir o cursor de navegação.", persistException);
                }
            }
        }
        catch (Exception ex)
        {
            AddonLog.Error("Falha ao executar ação de navegação por atalho.", ex);
            _client.RecordFailure("Não foi possível executar a navegação: " + ex.Message);
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    private void UnregisterAllUnsafe()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            foreach (var id in _registered.Keys.ToArray())
            {
                if (!UnregisterHotKey(_windowHandle, id))
                {
                    var error = Marshal.GetLastWin32Error();
                    AddonLog.Warn($"O Windows não conseguiu remover o registro de hotkey {id} (erro {error}).");
                }
            }
        }
        _registered.Clear();
    }

    public void Dispose()
    {
        EnsureOwnerThread();
        lock (_sync)
        {
            UnregisterAllUnsafe();
            _windowHandle = IntPtr.Zero;
        }
    }

    private sealed record RegisteredHotkey(
        string ActionId,
        string Label,
        bool Enabled,
        List<string> Modifiers,
        string Key,
        int? Page,
        NavigationDirection Direction);

    private enum NavigationDirection { None, Next, Previous }
}

internal sealed record HotkeyRegistrationStatus(
    string ActionId,
    string Label,
    bool Enabled,
    string Combination,
    bool Registered,
    string Message);
