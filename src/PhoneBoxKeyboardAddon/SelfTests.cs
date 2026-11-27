using System.Text.Json;
using System.Security.Cryptography;
using System.Reflection;
using System.Windows.Forms;

namespace PhoneBoxKeyboardAddon;

internal static class SelfTests
{
    internal static int Run()
    {
        var failures = new List<string>();
        var defaults = AddonSettings.CreateDefault();
        if (defaults.ShowTrayIcon) failures.Add("O ícone da bandeja deve iniciar oculto por padrão.");
        if (defaults.UiLanguage != "pt-BR") failures.Add("O idioma padrão da interface deve ser Português (Brasil).");
        if (defaults.SchemaVersion != 3) failures.Add("A configuração padrão deve usar o esquema v3.");

        // Older settings files do not contain showTrayIcon. They must migrate with
        // the feature disabled so existing silent-start behavior is preserved.
        var schemaV2 = JsonSerializer.Deserialize<AddonSettings>("{\"schemaVersion\":2}", AddonSettings.JsonOptions);
        if (schemaV2 is null || schemaV2.ShowTrayIcon || schemaV2.UiLanguage != "pt-BR")
            failures.Add("A migração de configurações v2 deve manter o ícone da bandeja desativado por padrão.");
        else
        {
            ConfigStore.Normalize(schemaV2);
            if (schemaV2.SchemaVersion != 3 || schemaV2.ShowTrayIcon)
                failures.Add("A migração de esquema v2 para v3 não preservou corretamente a preferência do ícone.");
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var htmlResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.Web.settings.html");
        using var cssResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.Web.settings.css");
        using var trayIconResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.Web.phonebox-keyboard.ico");
        using var artworkResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.Web.phonebox-keyboard.png");
        using var updateHelperResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.UpdateHelper.ps1");
        using var packageCleanupHelperResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.PackageCleanupHelper.ps1");
        using var installCleanupHelperResource = assembly.GetManifestResourceStream("PhoneBoxKeyboardAddon.InstallCleanupHelper.ps1");
        if (installCleanupHelperResource is null)
            failures.Add("O helper temporário de remoção da pasta de instalação não foi incorporado ao executável.");
        if (htmlResource is null || cssResource is null)
            failures.Add("Os recursos incorporados HTML/CSS do painel não foram encontrados.");
        if (trayIconResource is null || trayIconResource.Length < 1024 || artworkResource is null || artworkResource.Length < 1024)
            failures.Add("Os recursos do ícone personalizado do aplicativo não foram incorporados corretamente.");
        if (updateHelperResource is null || updateHelperResource.Length < 1000)
            failures.Add("O helper do atualizador não foi incorporado ao executável.");
        else
        {
            using var helperReader = new StreamReader(updateHelperResource);
            var helperText = helperReader.ReadToEnd();
            if (!helperText.Contains("Write-UpdateResult 'success'", StringComparison.Ordinal)
                || !helperText.Contains("Write-UpdateResult 'failure'", StringComparison.Ordinal)
                || !helperText.Contains("Abrir-Configuracoes.exe", StringComparison.Ordinal))
                failures.Add("O helper do atualizador não atualiza o launcher ou não registra os resultados de sucesso e falha.");
        }
        if (packageCleanupHelperResource is null || packageCleanupHelperResource.Length < 1000)
            failures.Add("O helper de validação final da pasta do pacote não foi incorporado ao executável.");
        else
        {
            using var cleanupReader = new StreamReader(packageCleanupHelperResource);
            var cleanupText = cleanupReader.ReadToEnd();
            if (!cleanupText.Contains("PhoneBoxKeyboardAddonPackageManifest:v1", StringComparison.Ordinal)
                || !cleanupText.Contains("Get-FileHash", StringComparison.Ordinal)
                || !cleanupText.Contains("Remove-Item -LiteralPath $resolvedPackageRoot", StringComparison.Ordinal))
                failures.Add("O helper de desinstalação não valida os hashes imediatamente antes da remoção.");
        }

        if (defaults.Bindings.Count != 10) failures.Add("A configuração padrão não contém dez posições de página.");
        for (var page = 1; page <= 10; page++)
        {
            var binding = defaults.Bindings.SingleOrDefault(b => b.Page == page);
            if (binding is null) { failures.Add($"Posição de página {page} ausente."); continue; }
            var expectedKey = page == 10 ? "Num0" : $"Num{page}";
            if (binding.Key != expectedKey) failures.Add($"Página {page}: tecla padrão incorreta.");
            if (!HotkeyCatalog.TryGetVirtualKey(binding.Key, out var vk))
                failures.Add($"Página {page}: tecla Numpad não foi mapeada.");
            else
            {
                var expectedVk = page == 10 ? 0x60u : (uint)(0x60 + page);
                if (vk != expectedVk) failures.Add($"Página {page}: código virtual incorreto.");
            }
            if (binding.Enabled != (page <= 7)) failures.Add($"Página {page}: estado inicial de ativação incorreto.");
        }

        if (HotkeyCatalog.DisplayName("Top1") != "1" || HotkeyCatalog.DisplayName("Top0") != "0")
            failures.Add("A exibição das teclas numéricas superiores não retornou texto corretamente.");
        if (HotkeyCatalog.DisplayName("Num1") != "Numpad 1" || HotkeyCatalog.DisplayName("Num0") != "Numpad 0")
            failures.Add("A exibição das teclas Numpad não está correta.");

        if (!HotkeyCatalog.TryGetModifiers(["Ctrl", "Shift"], out var modifiers, out _) || modifiers != 0x0006)
            failures.Add("Combinação Ctrl + Shift não foi mapeada corretamente.");
        if (!HotkeyCatalog.TryGetVirtualKey(defaults.NextPage.Key, out var nextVk) || nextVk != 0x6B)
            failures.Add("Numpad + não foi mapeado para Próxima página.");
        if (!HotkeyCatalog.TryGetVirtualKey(defaults.PreviousPage.Key, out var previousVk) || previousVk != 0x6D)
            failures.Add("Numpad - não foi mapeado para Página anterior.");
        if (PhoneBoxClient.RelativeNavigationPath(next: true) != "/api/goto/next"
            || PhoneBoxClient.RelativeNavigationPath(next: false) != "/api/goto/prev")
            failures.Add("Os atalhos relativos não apontam para os webhooks nativos do PhoneBox 0.9.31.");
        const string sampleHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        if (UpdateManager.ReadExpectedHash(sampleHash + "  PhoneBoxKeyboardAddon-win-x64.zip") != sampleHash)
            failures.Add("O verificador de atualização não reconheceu o checksum SHA-256 do pacote.");
        if (SettingsValidator.Validate(defaults).Count != 0)
            failures.Add("A configuração padrão não passou na validação.");

        // Migration regression: a v1 settings file had seven direct bindings and no
        // relative actions. Normalization must preserve them and add the new defaults.
        var legacyBindings = AddonSettings.CreatePageDefaults().Take(7).ToList();
        legacyBindings[0].Key = "A";
        legacyBindings[0].Modifiers = ["Alt"];
        var legacySettings = new AddonSettings
        {
            SchemaVersion = 1,
            Bindings = legacyBindings,
            NextPage = null!,
            PreviousPage = null!
        };
        ConfigStore.Normalize(legacySettings);
        if (legacySettings.SchemaVersion != 3 || legacySettings.Bindings.Count != 10
            || legacySettings.NextPage is null || legacySettings.PreviousPage is null
            || legacySettings.Bindings.Single(b => b.Page == 1).Key != "A"
            || legacySettings.Bindings.Single(b => b.Page == 1).Modifiers.Single() != "Alt"
            || legacySettings.Bindings.Any(b => b.Page is >= 8 and <= 10 && b.Enabled))
            failures.Add("A migração de configurações v1 não preservou combinações personalizadas, os sete atalhos antigos e os novos padrões desativados.");
        if (SettingsValidator.AllEnabledHotkeys(defaults).Count() != 9)
            failures.Add("A configuração padrão não habilitou sete atalhos diretos e as duas ações relativas.");
        var allTwelve = defaults.DeepClone();
        foreach (var binding in allTwelve.Bindings) binding.Enabled = true;
        if (SettingsValidator.AllEnabledHotkeys(allTwelve).Count() != 12 || SettingsValidator.Validate(allTwelve).Count != 0)
            failures.Add("Não foi possível habilitar simultaneamente os dez atalhos diretos e as duas ações relativas.");
        var withNullSlot = defaults.DeepClone();
        withNullSlot.Bindings[0] = null!;
        if (SettingsValidator.Validate(withNullSlot).Count == 0)
            failures.Add("Uma entrada nula na lista de atalhos deveria ser rejeitada sem lançar exceção.");

        var custom = defaults.DeepClone();
        custom.Bindings[0].Key = "A";
        custom.Bindings[0].Modifiers = ["Ctrl", "Shift"];
        custom.NextPage.Key = "ArrowRight";
        custom.NextPage.Modifiers = ["Alt", "Ctrl"];
        custom.PreviousPage.Enabled = false;
        if (SettingsValidator.Validate(custom).Count != 0)
            failures.Add("Combinações personalizadas válidas foram recusadas.");

        var duplicate = defaults.DeepClone();
        duplicate.Bindings[1].Key = duplicate.Bindings[0].Key;
        duplicate.Bindings[1].Modifiers = [.. duplicate.Bindings[0].Modifiers];
        if (!SettingsValidator.Validate(duplicate).Any(e => e.Contains("repetida", StringComparison.OrdinalIgnoreCase)))
            failures.Add("Combinações globais duplicadas não foram detectadas.");

        var duplicateNavigation = defaults.DeepClone();
        duplicateNavigation.NextPage.Key = duplicateNavigation.Bindings[0].Key;
        duplicateNavigation.NextPage.Modifiers = [.. duplicateNavigation.Bindings[0].Modifiers];
        if (!SettingsValidator.Validate(duplicateNavigation).Any(e => e.Contains("repetida", StringComparison.OrdinalIgnoreCase)))
            failures.Add("Colisão entre atalho de página e navegação relativa não foi detectada.");

        var external = defaults.DeepClone();
        external.PhoneBoxUrl = "https://example.com";
        if (SettingsValidator.Validate(external).Count == 0)
            failures.Add("Um endereço externo foi aceito como destino PhoneBox.");
        if (!SettingsValidator.TryNormalizePhoneBoxUrl(" http://127.0.0.1:8765/ ", out var normalizedUrl, out _)
            || normalizedUrl != "http://127.0.0.1:8765")
            failures.Add("A URL local do PhoneBox não foi normalizada corretamente.");
        if (SettingsValidator.TryNormalizePhoneBoxUrl("http://127.0.0.1:8765/api/push", out _, out _))
            failures.Add("Um caminho de API foi aceito como origem base do PhoneBox.");

        var traySettings = defaults.DeepClone();
        traySettings.ShowTrayIcon = true;
        traySettings.UiLanguage = "en";
        var json = JsonSerializer.Serialize(traySettings, AddonSettings.JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<AddonSettings>(json, AddonSettings.JsonOptions);
        if (roundTrip?.Bindings.Count != 10 || roundTrip.NextPage is null || roundTrip.PreviousPage is null
            || roundTrip.ShowTrayIcon != true || roundTrip.UiLanguage != "en")
            failures.Add("JSON serialization did not preserve the ten shortcuts, relative navigation, tray icon preference, and UI language.");
        var unsupportedLanguage = defaults.DeepClone();
        unsupportedLanguage.UiLanguage = "fr";
        ConfigStore.Normalize(unsupportedLanguage);
        if (unsupportedLanguage.UiLanguage != "pt-BR")
            failures.Add("Unsupported interface languages should fall back to Brazilian Portuguese.");

        // Regression test for the status panel: it consumes explicit lower-camel-case
        // fields and server-calculated totals rather than inferring counts from records.
        var sampleStatuses = new[]
        {
            new HotkeyRegistrationStatus("page-1", "Página 1", true, "Ctrl + Numpad 1", true, "Atalho registrado."),
            new HotkeyRegistrationStatus("page-2", "Página 2", true, "Ctrl + Numpad 2", false, "Combinação em conflito."),
            new HotkeyRegistrationStatus("page-3", "Página 3", false, "Ctrl + Numpad 3", false, "Atalho desativado.")
        };
        var statusPayload = new
        {
            registrations = sampleStatuses.Select(row => new
            {
                actionId = row.ActionId, label = row.Label, enabled = row.Enabled,
                combination = row.Combination, registered = row.Registered, message = row.Message
            }).ToArray(),
            enabledRegistrationCount = sampleStatuses.Count(row => row.Enabled),
            registeredCount = sampleStatuses.Count(row => row.Registered),
            actionCount = sampleStatuses.Length
        };
        using (var statusDocument = JsonDocument.Parse(JsonSerializer.Serialize(statusPayload, AddonSettings.JsonOptions)))
        {
            var root = statusDocument.RootElement;
            if (!root.TryGetProperty("registeredCount", out var registeredCount) || registeredCount.GetInt32() != 1
                || !root.TryGetProperty("enabledRegistrationCount", out var enabledCount) || enabledCount.GetInt32() != 2
                || !root.TryGetProperty("actionCount", out var actionCount) || actionCount.GetInt32() != 3
                || !root.GetProperty("registrations")[0].TryGetProperty("registered", out _)
                || !root.GetProperty("registrations")[0].TryGetProperty("enabled", out _))
                failures.Add("O contrato JSON de status não forneceu contagens ou nomes de campos explícitos em camelCase.");
        }
        if (UninstallScheduler.CanUninstall(Path.Combine(Path.GetTempPath(), "not-the-installed-addon.exe")))
            failures.Add("O desinstalador ofereceu remover um executável que não está na pasta instalada.");

        // The optional extracted-package cleanup must accept only a marked flat
        // package whose exact file inventory and SHA-256 values match its manifest.
        var packageTestDirectory = Path.Combine(Path.GetTempPath(), $"PhoneBox Keyboard & Add-on % teste {Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(packageTestDirectory);
            var packageFiles = new[]
            {
                "PhoneBoxKeyboardAddon.exe", "Abrir-Configuracoes.exe", "install.ps1",
                "uninstall.ps1", "README.md", "LICENSE"
            };
            foreach (var file in packageFiles)
                File.WriteAllText(Path.Combine(packageTestDirectory, file), "test");

            var manifest = new
            {
                format = PackageSource.ManifestFormat,
                files = packageFiles.Select(file => new
                {
                    name = file,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(packageTestDirectory, file))))
                }).ToArray()
            };
            File.WriteAllText(
                Path.Combine(packageTestDirectory, PackageSource.ManifestFileName),
                JsonSerializer.Serialize(manifest));
            File.WriteAllText(Path.Combine(packageTestDirectory, PackageSource.MarkerFileName), PackageSource.MarkerContent);
            if (!PackageSource.IsSafePackageDirectory(packageTestDirectory))
                failures.Add("Um pacote de release de teste válido foi recusado pela validação da pasta de origem.");

            var unexpectedFile = Path.Combine(packageTestDirectory, "arquivo-pessoal.txt");
            File.WriteAllText(unexpectedFile, "nao deve ser removido automaticamente");
            if (PackageSource.IsSafePackageDirectory(packageTestDirectory))
                failures.Add("A validação aceitou arquivos extras que não constam no manifesto do pacote.");
            File.Delete(unexpectedFile);

            File.AppendAllText(Path.Combine(packageTestDirectory, "README.md"), " alterado");
            if (PackageSource.IsSafePackageDirectory(packageTestDirectory))
                failures.Add("A validação aceitou um arquivo do pacote cujo SHA-256 foi alterado.");
            File.WriteAllText(Path.Combine(packageTestDirectory, "README.md"), "test");

            var nestedDirectory = Path.Combine(packageTestDirectory, "subpasta-nao-permitida");
            Directory.CreateDirectory(nestedDirectory);
            if (PackageSource.IsSafePackageDirectory(packageTestDirectory))
                failures.Add("A validação aceitou um pacote que contém uma subpasta inesperada.");
            Directory.Delete(nestedDirectory);

            Directory.CreateDirectory(Path.Combine(packageTestDirectory, ".git"));
            if (PackageSource.IsSafePackageDirectory(packageTestDirectory))
                failures.Add("A validação de desinstalação aceitou uma pasta que contém .git.");
        }
        catch (Exception ex)
        {
            failures.Add($"Não foi possível executar o teste de segurança da pasta do pacote: {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(packageTestDirectory)) Directory.Delete(packageTestDirectory, recursive: true); }
            catch (Exception ex) { failures.Add($"Não foi possível limpar a pasta temporária do teste: {ex.Message}"); }
        }

        // Regression test: worker requests must be marshalled to the HWND-owning thread.
        try
        {
            using var client = new PhoneBoxClient();
            var manager = new HotkeyManager(client, defaults);
            using var window = new HotkeyWindow(manager, () => { });
            manager.AttachWindow(window.Handle);
            var ownerThreadId = Environment.CurrentManagedThreadId;

            Task<int> dispatchTask = Task.Run(async () => await window.InvokeAsync(() => Environment.CurrentManagedThreadId));
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!dispatchTask.IsCompleted && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
            if (!dispatchTask.IsCompleted)
                failures.Add("A fila de despacho não entregou a operação à thread da janela em até 3 segundos.");
            else if (dispatchTask.GetAwaiter().GetResult() != ownerThreadId)
                failures.Add("A operação despachada não executou na thread proprietária da janela.");

            // Register a real set before suspending/resuming it. This exercises the
            // RegisterHotKey/UnregisterHotKey lifecycle, not just the dispatcher queue.
            var initialStatuses = manager.Apply(defaults);
            if (initialStatuses.Count != 12)
                failures.Add("A aplicação inicial não processou as doze ações de hotkey.");

            Task<bool> suspendTask = Task.Run(async () => await window.InvokeAsync(() =>
            {
                var statuses = manager.SetSuspended(true);
                return manager.IsSuspended && statuses.Count == 12 && statuses.All(status => !status.Registered);
            }));
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (!suspendTask.IsCompleted && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
            if (!suspendTask.IsCompleted || !suspendTask.GetAwaiter().GetResult())
                failures.Add("A suspensão não removeu os registros ativos na thread proprietária da janela.");

            Task<bool> resumeTask = Task.Run(async () => await window.InvokeAsync(() =>
            {
                var statuses = manager.SetSuspended(false);
                return !manager.IsSuspended && statuses.Count == 12;
            }));
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (!resumeTask.IsCompleted && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
            if (!resumeTask.IsCompleted || !resumeTask.GetAwaiter().GetResult())
                failures.Add("A reativação não reaplicou as doze ações na thread proprietária da janela.");

            var rejectedOffThread = Task.Run(() =>
            {
                try { manager.Apply(defaults); return false; }
                catch (InvalidOperationException) { return true; }
            }).GetAwaiter().GetResult();
            if (!rejectedOffThread)
                failures.Add("O gerenciador não bloqueou uma tentativa de registrar hotkeys fora da thread proprietária.");

            window.InvokeAsync(() => { manager.Dispose(); return true; }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            failures.Add("O teste de afinidade/dispatch da janela falhou: " + ex.Message);
        }

        if (failures.Count == 0)
        {
            AddonLog.Info("Self-test concluído: dez slots, navegação relativa nativa, mapeamento de teclas, validação, contrato JSON de status, recursos do ícone, desinstalação segura, registro/suspensão/reativação de hotkeys e despacho de thread aprovados.");
            return 0;
        }

        foreach (var failure in failures) AddonLog.Error("Self-test: " + failure);
        return failures.Count;
    }
}
