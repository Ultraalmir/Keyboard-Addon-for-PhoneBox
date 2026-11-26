using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PhoneBoxKeyboardAddon;

/// <summary>Small loopback-only HTTP server for the self-contained HTML settings page.</summary>
internal sealed class LocalSettingsServer : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 64 * 1024;
    private readonly ConfigStore _store;
    private readonly HotkeyManager _hotkeys;
    private readonly PhoneBoxClient _phoneBox;
    private readonly HotkeyWindow _window;
    private readonly TrayIconController _trayIcon;
    private TcpListener? _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly SemaphoreSlim _configGate = new(1, 1);
    private CancellationTokenSource? _captureLease;
    private static readonly TimeSpan CaptureLeaseDuration = TimeSpan.FromSeconds(60);
    private Task? _acceptLoop;
    private bool _started;
    private int _port;
    internal string SettingsUrl => $"http://127.0.0.1:{_port}/";

    internal LocalSettingsServer(ConfigStore store, HotkeyManager hotkeys, PhoneBoxClient phoneBox, HotkeyWindow window, TrayIconController trayIcon)
    {
        _store = store;
        _hotkeys = hotkeys;
        _phoneBox = phoneBox;
        _window = window;
        _trayIcon = trayIcon;
    }

    internal void Start()
    {
        Directory.CreateDirectory(AddonPaths.DataDirectory);
        for (var port = Program.DefaultSettingsPort; port <= Program.DefaultSettingsPort + 20; port++)
        {
            var candidate = new TcpListener(IPAddress.Loopback, port);
            try
            {
                candidate.Start(backlog: 16);
                _listener = candidate;
                _port = port;
                File.WriteAllText(AddonPaths.ControlPortFile, port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            }
            catch (SocketException)
            {
                candidate.Stop();
            }
        }

        if (_listener is null)
            throw new InvalidOperationException("Não foi possível reservar uma porta local para as configurações (47831–47851). Feche o aplicativo que está usando essas portas e tente novamente.");

        _started = true;
        _acceptLoop = Task.Run(AcceptLoopAsync);
        AddonLog.Info($"Painel local de configurações disponível em {SettingsUrl}");
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(_stop.Token);
                _ = Task.Run(() => HandleClientAsync(client, _stop.Token));
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                if (!_stop.IsCancellationRequested) AddonLog.Error("Erro no servidor local de configurações.", ex);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, cancellationToken);
                if (request is null) return;

                var expectedHost = $"127.0.0.1:{_port}";
                if (!request.Headers.TryGetValue("Host", out var host)
                    || !host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonAsync(stream, 403, new { error = "Host não permitido." }, cancellationToken);
                    return;
                }

                if (request.Headers.TryGetValue("Origin", out var origin)
                    && !origin.Equals($"http://127.0.0.1:{_port}", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonAsync(stream, 403, new { error = "Origem não permitida." }, cancellationToken);
                    return;
                }

                await RouteAsync(stream, request, cancellationToken);
            }
            catch (HttpInputException ex)
            {
                try { await WriteJsonAsync(client.GetStream(), ex.StatusCode, new { error = ex.Message }, cancellationToken); }
                catch { }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                AddonLog.Error("Erro ao processar solicitação do painel local.", ex);
                try { await WriteJsonAsync(client.GetStream(), 500, new { error = "Erro interno do complemento." }, cancellationToken); }
                catch { }
            }
        }
    }

    private async Task RouteAsync(NetworkStream stream, HttpRequestData request, CancellationToken cancellationToken)
    {
        var path = request.Path;
        if (request.Method == "GET" && (path == "/" || path == "/settings"))
        {
            await WriteHtmlAsync(stream, ReadSettingsHtml(), cancellationToken);
            return;
        }

        if (request.Method == "GET" && path == "/settings.css")
        {
            await WriteCssAsync(stream, ReadSettingsCss(), cancellationToken);
            return;
        }

        // Serve the same embedded artwork used by the Windows executable and tray icon.
        // The settings page references these local routes as its favicon and brand mark.
        if (request.Method == "GET" && path == "/favicon.ico")
        {
            await WriteStaticAssetAsync(stream, "image/x-icon",
                ReadEmbeddedBytes("PhoneBoxKeyboardAddon.Web.phonebox-keyboard.ico", "O ícone ICO não foi incorporado ao executável."),
                cancellationToken);
            return;
        }

        if (request.Method == "GET" && path == "/phonebox-keyboard.png")
        {
            await WriteStaticAssetAsync(stream, "image/png",
                ReadEmbeddedBytes("PhoneBoxKeyboardAddon.Web.phonebox-keyboard.png", "O ícone PNG não foi incorporado ao executável."),
                cancellationToken);
            return;
        }

        if (request.Method == "GET" && path == "/api/config")
        {
            await WriteJsonAsync(stream, 200, _store.Load(), cancellationToken);
            return;
        }

        if (request.Method == "GET" && path == "/api/status")
        {
            var last = _phoneBox.LastResult;
            var savedSettings = _store.Load();
            var statusRows = _hotkeys.Status;
            // Publish an explicit lower-camel-case contract. The panel also consumes
            // authoritative totals so a naming-policy mismatch can never show 0 of 0.
            var registrations = statusRows.Select(row => new
            {
                actionId = row.ActionId,
                label = row.Label,
                enabled = row.Enabled,
                combination = row.Combination,
                registered = row.Registered,
                message = row.Message
            }).ToArray();
            var payload = new
            {
                name = ProductInfo.Name,
                version = ProductInfo.Version,
                running = true,
                settingsUrl = SettingsUrl,
                settingsFile = AddonPaths.SettingsFile,
                logFile = AddonPaths.LogFile,
                trayIconVisible = savedSettings.ShowTrayIcon,
                shortcutsEnabled = savedSettings.ShortcutsEnabled,
                registrations,
                enabledRegistrationCount = statusRows.Count(row => row.Enabled),
                registeredCount = statusRows.Count(row => row.Registered),
                actionCount = statusRows.Count,
                capturing = _hotkeys.IsSuspended,
                navigationCursorPage = _hotkeys.NavigationCursorPage,
                canUninstall = UninstallScheduler.CanUninstall(Program.ExecutablePath),
                lastAction = new { message = last.Message, at = last.At, success = last.Success }
            };
            await WriteJsonAsync(stream, 200, payload, cancellationToken);
            return;
        }

        if (request.Method == "GET" && path == "/api/pages")
        {
            var result = await _phoneBox.GetPagesAsync(_store.Load(), forceRefresh: true);
            await WriteJsonAsync(stream, result.Success ? 200 : 503,
                new { success = result.Success, message = result.Message, count = result.Pages.Count,
                    cursorPage = _hotkeys.NavigationCursorPage, pages = result.Pages }, cancellationToken);
            return;
        }

        if (request.Method == "GET" && path == "/api/connection")
        {
            var result = await _phoneBox.CheckConnectionAsync(_store.Load());
            await WriteJsonAsync(stream, result.Success ? 200 : 503,
                new { success = result.Success, message = result.Message }, cancellationToken);
            return;
        }

        if (request.Method == "POST" && path == "/api/connection")
        {
            var data = DeserializeBody<ConnectionRequest>(request.Body);
            var candidate = _store.Load();
            candidate.PhoneBoxUrl = data.PhoneBoxUrl?.Trim() ?? "";
            var errors = SettingsValidator.Validate(candidate);
            if (errors.Count > 0) throw new HttpInputException(400, string.Join(" ", errors));
            if (SettingsValidator.TryNormalizePhoneBoxUrl(candidate.PhoneBoxUrl, out var normalizedPhoneBoxUrl, out _))
                candidate.PhoneBoxUrl = normalizedPhoneBoxUrl;
            var result = await _phoneBox.CheckConnectionAsync(candidate);
            await WriteJsonAsync(stream, result.Success ? 200 : 503,
                new { success = result.Success, message = result.Message }, cancellationToken);
            return;
        }

        if (request.Method == "POST" && path == "/api/config")
        {
            AddonSettings? settings;
            try { settings = JsonSerializer.Deserialize<AddonSettings>(request.Body, AddonSettings.JsonOptions); }
            catch (JsonException) { throw new HttpInputException(400, "JSON de configuração inválido."); }
            if (settings is null) throw new HttpInputException(400, "Configuração vazia.");
            var errors = SettingsValidator.Validate(settings);
            if (errors.Count > 0) throw new HttpInputException(400, string.Join(" ", errors));

            await _configGate.WaitAsync(cancellationToken);
            try
            {
                if (SettingsValidator.TryNormalizePhoneBoxUrl(settings.PhoneBoxUrl, out var normalizedPhoneBoxUrl, out _))
                    settings.PhoneBoxUrl = normalizedPhoneBoxUrl;
                _store.Save(settings);
                // The hotkey HWND belongs to the WinForms UI thread. Never call
                // RegisterHotKey/UnregisterHotKey from an HTTP worker thread.
                var registrations = await _window.InvokeAsync(() =>
                {
                    var applied = _hotkeys.Apply(settings);
                    _trayIcon.SetShortcutsEnabled(settings.ShortcutsEnabled);
                    _trayIcon.SetVisible(settings.ShowTrayIcon);
                    return applied;
                });
                string? startupWarning = null;
                try { StartupManager.SetEnabled(settings.StartWithWindows); }
                catch (Exception ex)
                {
                    startupWarning = "As configurações foram salvas, mas não foi possível atualizar a inicialização automática: " + ex.Message;
                    AddonLog.Error("Falha ao atualizar a inicialização automática.", ex);
                }

                var failed = registrations.Count(r => r.Enabled && !r.Registered);
                await WriteJsonAsync(stream, 200, new
                {
                    success = true,
                    message = failed == 0 ? "Configurações salvas e aplicadas." : $"Configurações salvas. {failed} atalho(s) não puderam ser registrados.",
                    registrations,
                    warning = startupWarning
                }, cancellationToken);
            }
            finally
            {
                _configGate.Release();
            }
            return;
        }

        if (request.Method == "POST" && path == "/api/capture")
        {
            var data = DeserializeBody<CaptureRequest>(request.Body);
            if (data.Active) await BeginCaptureAsync();
            else await EndCaptureAsync();
            var capturing = _hotkeys.IsSuspended;

            await WriteJsonAsync(stream, 200, new
            {
                success = true,
                capturing,
                message = capturing
                    ? "Atalhos suspensos temporariamente para capturar uma combinação."
                    : "Atalhos reativados."
            }, cancellationToken);
            return;
        }

        if (request.Method == "POST" && path == "/api/test")
        {
            var data = DeserializeBody<TestRequest>(request.Body);
            if (data.Page is < 1 or > 10) throw new HttpInputException(400, "A página de teste deve estar entre 1 e 10.");
            var settings = _store.Load();
            var pages = await _phoneBox.GetPagesAsync(settings, forceRefresh: true);
            var target = pages.Pages.FirstOrDefault(p => p.Position == data.Page);
            if (!pages.Success || target is null)
            {
                await WriteJsonAsync(stream, 503,
                    new { success = false, message = pages.Message }, cancellationToken);
                return;
            }
            var result = await _phoneBox.NavigateAsync(settings, target.Id, target.Title);
            await WriteJsonAsync(stream, result.Success ? 200 : 503,
                new { success = result.Success, message = result.Message }, cancellationToken);
            return;
        }

        if (request.Method == "POST" && path == "/api/shutdown")
        {
            await WriteJsonAsync(stream, 200, new { success = true, message = "Complemento encerrado." }, cancellationToken);
            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                _window.RequestShutdown();
            });
            return;
        }

        if (request.Method == "POST" && path == "/api/uninstall")
        {
            var data = DeserializeBody<UninstallRequest>(request.Body);
            if (!UninstallScheduler.CanUninstall(Program.ExecutablePath))
                throw new HttpInputException(409, "O complemento não está sendo executado a partir da pasta instalada. Use o instalador para reinstalar e abra as configurações pelo Menu Iniciar.");

            // The cleanup helper waits for this process to exit, then removes the
            // installed executable, shortcuts and optionally user data.
            var cleanupScript = UninstallScheduler.Schedule(data.RemoveUserData);
            await WriteJsonAsync(stream, 200, new
            {
                success = true,
                message = data.RemoveUserData
                    ? "Desinstalação agendada. O aplicativo, as configurações e os logs serão removidos."
                    : "Desinstalação agendada. As configurações e os logs serão preservados.",
                cleanup = Path.GetFileName(cleanupScript)
            }, cancellationToken);
            _ = Task.Run(async () =>
            {
                await Task.Delay(350);
                _window.RequestShutdown();
            });
            return;
        }

        await WriteJsonAsync(stream, 404, new { error = "Rota não encontrada." }, cancellationToken);
    }

    private async Task BeginCaptureAsync()
    {
        await _captureGate.WaitAsync(_stop.Token);
        CancellationTokenSource? lease = null;
        try
        {
            // A repeated capture renews the 60-second lease. The previous timer
            // observes cancellation and disposes its own token source.
            CancelLease(_captureLease);
            lease = new CancellationTokenSource();
            _captureLease = lease;
            await _window.InvokeAsync(() => _hotkeys.SetSuspended(true));
        }
        catch
        {
            if (lease is not null)
            {
                if (ReferenceEquals(_captureLease, lease)) _captureLease = null;
                CancelLease(lease);
                lease.Dispose();
            }
            throw;
        }
        finally
        {
            _captureGate.Release();
        }

        _ = ResumeAfterCaptureLeaseAsync(lease!);
        AddonLog.Info($"Modo de captura iniciado; os atalhos serão reativados automaticamente em {CaptureLeaseDuration.TotalSeconds:0} segundos.");
    }

    private async Task EndCaptureAsync()
    {
        await _captureGate.WaitAsync(_stop.Token);
        try
        {
            var lease = _captureLease;
            _captureLease = null;
            CancelLease(lease);
            if (_hotkeys.IsSuspended)
                await _window.InvokeAsync(() => _hotkeys.SetSuspended(false));
        }
        finally
        {
            _captureGate.Release();
        }
    }

    private static void CancelLease(CancellationTokenSource? lease)
    {
        try { lease?.Cancel(); }
        catch (ObjectDisposedException) { /* The lease timer already completed. */ }
    }

    private async Task ResumeAfterCaptureLeaseAsync(CancellationTokenSource lease)
    {
        try
        {
            await Task.Delay(CaptureLeaseDuration, lease.Token);
            await _captureGate.WaitAsync(_stop.Token);
            try
            {
                if (ReferenceEquals(_captureLease, lease))
                {
                    _captureLease = null;
                    await _window.InvokeAsync(() => _hotkeys.SetSuspended(false));
                    AddonLog.Warn("O modo de captura expirou; os atalhos foram reativados automaticamente.");
                }
            }
            finally
            {
                _captureGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // The settings page ended capture, a new capture renewed the lease,
            // or the add-on is shutting down.
        }
        catch (ObjectDisposedException)
        {
            // The owning message window is already shutting down.
        }
        catch (Exception ex)
        {
            AddonLog.Error("Falha ao restaurar os atalhos após o modo de captura.", ex);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private static T DeserializeBody<T>(string body) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(body, AddonSettings.JsonOptions) ?? throw new HttpInputException(400, "Corpo vazio."); }
        catch (JsonException) { throw new HttpInputException(400, "JSON inválido."); }
    }

    private static string ReadSettingsHtml() => ReadEmbeddedText("PhoneBoxKeyboardAddon.Web.settings.html",
        "A página HTML não foi incorporada ao executável.");

    private static string ReadSettingsCss() => ReadEmbeddedText("PhoneBoxKeyboardAddon.Web.settings.css",
        "O arquivo CSS não foi incorporado ao executável.");

    private static byte[] ReadEmbeddedBytes(string resourceName, string errorMessage)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(errorMessage);
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static Task WriteStaticAssetAsync(NetworkStream stream, string contentType, byte[] body,
        CancellationToken cancellationToken)
    {
        return WriteResponseAsync(stream, 200, contentType, body,
            "Cache-Control: no-store\r\n", cancellationToken);
    }

    private static string ReadEmbeddedText(string resourceName, string errorMessage)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(errorMessage);
        using var reader = new StreamReader(resource, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static async Task<HttpRequestData?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var buffer = new byte[4096];
        var headerEnd = -1;
        while (headerEnd < 0)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) return null;
            received.AddRange(buffer.AsSpan(0, count).ToArray());
            if (received.Count > MaxHeaderBytes) throw new HttpInputException(413, "Cabeçalho HTTP muito grande.");
            headerEnd = FindHeaderEnd(received);
        }

        var bytes = received.ToArray();
        var headerText = Encoding.ASCII.GetString(bytes, 0, headerEnd);
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0) throw new HttpInputException(400, "Solicitação HTTP inválida.");
        var first = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (first.Length != 3 || !first[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            throw new HttpInputException(400, "Linha inicial HTTP inválida.");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        if (headers.ContainsKey("Transfer-Encoding")) throw new HttpInputException(400, "Transfer-Encoding não é aceito.");
        var contentLength = 0;
        if (headers.TryGetValue("Content-Length", out var lengthText)
            && (!int.TryParse(lengthText, out contentLength) || contentLength < 0 || contentLength > MaxBodyBytes))
            throw new HttpInputException(413, "Corpo HTTP muito grande ou inválido.");

        var bodyStart = headerEnd + 4;
        var body = new byte[contentLength];
        var alreadyBuffered = Math.Min(contentLength, bytes.Length - bodyStart);
        if (alreadyBuffered > 0) Array.Copy(bytes, bodyStart, body, 0, alreadyBuffered);
        var offset = alreadyBuffered;
        while (offset < contentLength)
        {
            var count = await stream.ReadAsync(body.AsMemory(offset, contentLength - offset), cancellationToken);
            if (count == 0) throw new HttpInputException(400, "Corpo HTTP incompleto.");
            offset += count;
        }

        var rawPath = first[1];
        var question = rawPath.IndexOf('?');
        var path = question >= 0 ? rawPath[..question] : rawPath;
        return new HttpRequestData(first[0].ToUpperInvariant(), Uri.UnescapeDataString(path), headers,
            Encoding.UTF8.GetString(body));
    }

    private static int FindHeaderEnd(List<byte> bytes)
    {
        for (var i = 3; i < bytes.Count; i++)
        {
            if (bytes[i - 3] == 13 && bytes[i - 2] == 10 && bytes[i - 1] == 13 && bytes[i] == 10)
                return i - 3;
        }
        return -1;
    }

    private static async Task WriteHtmlAsync(NetworkStream stream, string html, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(html);
        await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", body,
            "Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'\r\n",
            cancellationToken);
    }

    private static async Task WriteCssAsync(NetworkStream stream, string css, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(css);
        await WriteResponseAsync(stream, 200, "text/css; charset=utf-8", body,
            "Cache-Control: no-store\r\n",
            cancellationToken);
    }

    private static Task WriteJsonAsync(NetworkStream stream, int status, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, AddonSettings.JsonOptions);
        return WriteResponseAsync(stream, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json),
            "Cache-Control: no-store\r\n", cancellationToken);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string contentType, byte[] body,
        string extraHeaders, CancellationToken cancellationToken)
    {
        var reason = status switch
        {
            200 => "OK", 400 => "Bad Request", 403 => "Forbidden", 404 => "Not Found",
            413 => "Payload Too Large", 500 => "Internal Server Error", 503 => "Service Unavailable", _ => "OK"
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\n" +
            "X-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\n" + extraHeaders + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header.AsMemory(), cancellationToken);
        if (body.Length > 0) await stream.WriteAsync(body.AsMemory(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        // The app context unregisters hotkeys immediately after the server is
        // disposed. Cancel the lease so its timer cannot try to touch a closing HWND.
        _stop.Cancel();
        var lease = Interlocked.Exchange(ref _captureLease, null);
        CancelLease(lease);
        if (!_started) return;
        _listener?.Stop();
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try
        {
            if (File.Exists(AddonPaths.ControlPortFile)
                && File.ReadAllText(AddonPaths.ControlPortFile).Trim() == _port.ToString(System.Globalization.CultureInfo.InvariantCulture))
                File.Delete(AddonPaths.ControlPortFile);
        }
        catch { }
        // Outstanding HTTP/capture tasks may still be observing the cancellation token.
        // Leave these tiny synchronization objects for process teardown rather than
        // racing Dispose() against their finally blocks.
        _started = false;
    }

    private sealed record HttpRequestData(string Method, string Path, Dictionary<string, string> Headers, string Body);
    private sealed class TestRequest { public int Page { get; set; } }
    private sealed class CaptureRequest { public bool Active { get; set; } }
    private sealed class ConnectionRequest { public string? PhoneBoxUrl { get; set; } }
    private sealed class HttpInputException(int statusCode, string message) : Exception(message)
    {
        internal int StatusCode { get; } = statusCode;
    }
}

internal sealed class UninstallRequest { public bool RemoveUserData { get; set; } }
