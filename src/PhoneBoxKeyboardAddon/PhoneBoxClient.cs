using System.Text.Json;

namespace PhoneBoxKeyboardAddon;

internal sealed class PhoneBoxClient : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(4)
    };
    private readonly object _sync = new();
    private readonly object _pagesSync = new();
    private string? _pagesCacheUrl;
    private DateTimeOffset _pagesCacheAt;
    private IReadOnlyList<PhoneBoxPage> _pagesCache = [];
    private static readonly TimeSpan PagesCacheDuration = TimeSpan.FromSeconds(10);
    private string _lastMessage = "Aguardando primeiro atalho.";
    private DateTimeOffset? _lastActivity;
    private bool? _lastSuccess;

    internal (string Message, DateTimeOffset? At, bool? Success) LastResult
    {
        get { lock (_sync) return (_lastMessage, _lastActivity, _lastSuccess); }
    }

    internal async Task<(bool Success, string Message)> NavigateAsync(AddonSettings settings, string pageReference, string? pageTitle = null)
    {
        if (string.IsNullOrWhiteSpace(pageReference))
            return (false, "O identificador da página está vazio.");

        try
        {
            var baseUrl = settings.PhoneBoxUrl.TrimEnd('/');
            var escapedPage = Uri.EscapeDataString(pageReference);
            using var response = await _http.PostAsync($"{baseUrl}/api/goto/{escapedPage}?countdown=0", content: null);
            if (response.IsSuccessStatusCode)
            {
                var label = string.IsNullOrWhiteSpace(pageTitle) ? pageReference : pageTitle;
                SetResult(true, $"Solicitação enviada para {label}.");
                AddonLog.Info($"Navegação solicitada: {label} (ID {pageReference}).");
                return (true, $"Navegação enviada para {label}.");
            }

            var details = $"PhoneBox respondeu {(int)response.StatusCode} ({response.StatusCode}).";
            SetResult(false, details);
            AddonLog.Warn(details);
            return (false, details);
        }
        catch (TaskCanceledException)
        {
            const string message = "Tempo esgotado ao contatar o agente PhoneBox.";
            SetResult(false, message);
            return (false, message);
        }
        catch (HttpRequestException ex)
        {
            var message = "Não foi possível conectar ao PhoneBox. Verifique se o agente está em execução.";
            SetResult(false, message);
            AddonLog.Warn($"Falha de conexão com PhoneBox: {ex.Message}");
            return (false, message);
        }
        catch (Exception ex)
        {
            SetResult(false, ex.Message);
            AddonLog.Error("Erro ao enviar navegação ao PhoneBox.", ex);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Reads the public PhoneBox configuration to determine the enabled page order/count.
    /// This does not expose credentials or private configuration; the documented public API
    /// returns only the configuration sent to the connected panel.
    /// </summary>
    internal async Task<(bool Success, string Message, IReadOnlyList<PhoneBoxPage> Pages)> GetPagesAsync(AddonSettings settings, bool forceRefresh = false)
    {
        var baseUrl = settings.PhoneBoxUrl.TrimEnd('/');
        if (!forceRefresh)
        {
            lock (_pagesSync)
            {
                if (_pagesCacheUrl == baseUrl && DateTimeOffset.UtcNow - _pagesCacheAt < PagesCacheDuration && _pagesCache.Count > 0)
                    return (true, $"{_pagesCache.Count} página(s) habilitada(s) encontradas no PhoneBox.", _pagesCache);
            }
        }

        try
        {
            using var response = await _http.GetAsync(baseUrl + "/api/config");
            if (!response.IsSuccessStatusCode)
            {
                var message = $"Não foi possível consultar as páginas do PhoneBox ({(int)response.StatusCode}).";
                return (false, message, []);
            }

            await using var content = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(content);
            if (!document.RootElement.TryGetProperty("config", out var config)
                || !config.TryGetProperty("pages", out var pagesElement)
                || pagesElement.ValueKind != JsonValueKind.Array)
            {
                return (false, "A resposta do PhoneBox não contém a lista de páginas esperada.", []);
            }

            var pages = new List<PhoneBoxPage>();
            foreach (var page in pagesElement.EnumerateArray())
            {
                if (page.TryGetProperty("enabled", out var enabled)
                    && enabled.ValueKind == JsonValueKind.False)
                    continue;

                var id = page.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString() ?? ""
                    : "";
                var title = page.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
                    ? titleElement.GetString() ?? ""
                    : "";
                if (!string.IsNullOrWhiteSpace(id))
                    pages.Add(new PhoneBoxPage(pages.Count + 1, id, string.IsNullOrWhiteSpace(title) ? $"Página {pages.Count + 1}" : title));
            }

            if (pages.Count == 0)
                return (false, "O PhoneBox não informou nenhuma página habilitada.", []);

            var snapshot = pages.ToArray();
            lock (_pagesSync)
            {
                _pagesCacheUrl = baseUrl;
                _pagesCacheAt = DateTimeOffset.UtcNow;
                _pagesCache = snapshot;
            }
            return (true, $"{pages.Count} página(s) habilitada(s) encontradas no PhoneBox.", snapshot);
        }
        catch (JsonException ex)
        {
            AddonLog.Warn($"A configuração pública do PhoneBox não pôde ser interpretada: {ex.Message}");
            return (false, "A resposta do PhoneBox não é um JSON válido.", []);
        }
        catch (TaskCanceledException)
        {
            return (false, "Tempo esgotado ao consultar as páginas do PhoneBox.", []);
        }
        catch (HttpRequestException ex)
        {
            AddonLog.Warn($"Falha ao consultar as páginas do PhoneBox: {ex.Message}");
            return (false, "Não foi possível conectar ao agente PhoneBox para listar as páginas.", []);
        }
    }

    internal async Task<(bool Success, string Message)> CheckConnectionAsync(AddonSettings settings)
    {
        try
        {
            using var response = await _http.GetAsync(settings.PhoneBoxUrl.TrimEnd('/') + "/api/push");
            if (response.IsSuccessStatusCode) return (true, "Agente PhoneBox acessível em " + settings.PhoneBoxUrl + ".");
            return (false, $"O agente respondeu {(int)response.StatusCode} ({response.StatusCode}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, "Não foi possível conectar ao agente PhoneBox em " + settings.PhoneBoxUrl + ".");
        }
    }

    internal void RecordFailure(string message)
    {
        SetResult(false, message);
        AddonLog.Warn(message);
    }

    private void SetResult(bool success, string message)
    {
        lock (_sync)
        {
            _lastSuccess = success;
            _lastMessage = message;
            _lastActivity = DateTimeOffset.Now;
        }
    }

    public void Dispose() => _http.Dispose();
}

internal sealed record PhoneBoxPage(int Position, string Id, string Title);
