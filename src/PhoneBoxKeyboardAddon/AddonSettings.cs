using System.Text.Json;

namespace PhoneBoxKeyboardAddon;

internal sealed class AddonSettings
{
    public int SchemaVersion { get; set; } = 3;
    public string UiLanguage { get; set; } = "pt-BR";
    public bool ShortcutsEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowTrayIcon { get; set; }
    public string PhoneBoxUrl { get; set; } = "http://127.0.0.1:8765";
    public int NavigationStartPage { get; set; } = 1;
    public List<HotkeyBinding> Bindings { get; set; } = CreatePageDefaults();
    public NavigationHotkey NextPage { get; set; } = CreateNextDefault();
    public NavigationHotkey PreviousPage { get; set; } = CreatePreviousDefault();

    internal static List<HotkeyBinding> CreatePageDefaults() => Enumerable.Range(1, 10)
        .Select(page => new HotkeyBinding
        {
            Page = page,
            // Preserve the original seven active shortcuts. Slots 8–10 are ready to configure,
            // but stay disabled until the user assigns a page that exists in PhoneBox.
            Enabled = page <= 7,
            Modifiers = ["Ctrl"],
            Key = page == 10 ? "Num0" : $"Num{page}"
        }).ToList();

    internal static NavigationHotkey CreateNextDefault() => new()
    {
        Enabled = true,
        Modifiers = ["Ctrl"],
        Key = "NumAdd"
    };

    internal static NavigationHotkey CreatePreviousDefault() => new()
    {
        Enabled = true,
        Modifiers = ["Ctrl"],
        Key = "NumSubtract"
    };

    internal static AddonSettings CreateDefault() => new();

    internal AddonSettings DeepClone() => JsonSerializer.Deserialize<AddonSettings>(
        JsonSerializer.Serialize(this, JsonOptions), JsonOptions) ?? CreateDefault();

    internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}

internal sealed class HotkeyBinding
{
    public int Page { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string> Modifiers { get; set; } = ["Ctrl"];
    public string Key { get; set; } = "Num1";
}

internal sealed class NavigationHotkey
{
    public bool Enabled { get; set; } = true;
    public List<string> Modifiers { get; set; } = ["Ctrl"];
    public string Key { get; set; } = "NumAdd";
}

internal sealed class ConfigStore
{
    private readonly object _sync = new();

    internal AddonSettings Load()
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(AddonPaths.DataDirectory);
                if (!File.Exists(AddonPaths.SettingsFile))
                {
                    var defaults = AddonSettings.CreateDefault();
                    SaveUnsafe(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(AddonPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<AddonSettings>(json, AddonSettings.JsonOptions)
                    ?? AddonSettings.CreateDefault();
                // Compare the serialized representation before and after normalization. This
                // persists migrations and repairs (missing slots, duplicate positions, bad modifiers,
                // null navigation bindings, normalized URL) rather than keeping them only in memory.
                var original = JsonSerializer.Serialize(settings, AddonSettings.JsonOptions);
                Normalize(settings);
                var normalized = JsonSerializer.Serialize(settings, AddonSettings.JsonOptions);
                if (!string.Equals(original, normalized, StringComparison.Ordinal)) SaveUnsafe(settings);
                return settings;
            }
            catch (Exception ex)
            {
                AddonLog.Error("Não foi possível ler settings.json; as configurações padrão serão restauradas.", ex);
                try
                {
                    if (File.Exists(AddonPaths.SettingsFile))
                    {
                        var backup = AddonPaths.SettingsFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        File.Copy(AddonPaths.SettingsFile, backup, overwrite: false);
                    }
                }
                catch (Exception backupEx) { AddonLog.Warn($"Não foi possível criar backup: {backupEx.Message}"); }

                var defaults = AddonSettings.CreateDefault();
                try { SaveUnsafe(defaults); } catch (Exception saveEx) { AddonLog.Error("Falha ao restaurar configurações padrão.", saveEx); }
                return defaults;
            }
        }
    }

    internal void Save(AddonSettings settings)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(AddonPaths.DataDirectory);
            Normalize(settings);
            SaveUnsafe(settings);
        }
    }

    internal void SaveNavigationCursor(int page)
    {
        lock (_sync)
        {
            // Load() uses the same re-entrant monitor, so this read/modify/write cannot
            // overwrite concurrent keybinding changes submitted by the settings panel.
            var settings = Load();
            settings.NavigationStartPage = Math.Clamp(page, 1, 500);
            Normalize(settings);
            SaveUnsafe(settings);
        }
    }

    private static void SaveUnsafe(AddonSettings settings)
    {
        var temp = AddonPaths.SettingsFile + ".tmp";
        var json = JsonSerializer.Serialize(settings, AddonSettings.JsonOptions);
        File.WriteAllText(temp, json, new System.Text.UTF8Encoding(false));
        File.Move(temp, AddonPaths.SettingsFile, overwrite: true);
    }

    internal static void Normalize(AddonSettings settings)
    {
        settings.SchemaVersion = 3;
        settings.UiLanguage = string.Equals(settings.UiLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "pt-BR";
        settings.PhoneBoxUrl = SettingsValidator.TryNormalizePhoneBoxUrl(settings.PhoneBoxUrl, out var normalizedUrl, out _)
            ? normalizedUrl
            : "http://127.0.0.1:8765";
        settings.NavigationStartPage = Math.Clamp(settings.NavigationStartPage, 1, 500);
        settings.Bindings ??= [];
        settings.Bindings = settings.Bindings
            .Where(b => b is not null && b.Page is >= 1 and <= 10)
            .GroupBy(b => b.Page)
            .Select(g => g.First())
            .ToList();

        foreach (var defaultBinding in AddonSettings.CreatePageDefaults())
        {
            if (settings.Bindings.All(b => b.Page != defaultBinding.Page)) settings.Bindings.Add(defaultBinding);
        }

        settings.Bindings = settings.Bindings.OrderBy(b => b.Page).ToList();
        foreach (var binding in settings.Bindings)
            NormalizeHotkey(binding);

        settings.NextPage ??= AddonSettings.CreateNextDefault();
        settings.PreviousPage ??= AddonSettings.CreatePreviousDefault();
        NormalizeHotkey(settings.NextPage);
        NormalizeHotkey(settings.PreviousPage);
    }

    private static void NormalizeHotkey(HotkeyBinding binding)
    {
        binding.Key ??= "";
        binding.Modifiers = (binding.Modifiers ?? [])
            .Where(m => HotkeyCatalog.ValidModifiers.Contains(m, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void NormalizeHotkey(NavigationHotkey binding)
    {
        binding.Key ??= "";
        binding.Modifiers = (binding.Modifiers ?? [])
            .Where(m => HotkeyCatalog.ValidModifiers.Contains(m, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
