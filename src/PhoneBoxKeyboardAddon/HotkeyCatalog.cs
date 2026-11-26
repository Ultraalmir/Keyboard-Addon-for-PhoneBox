namespace PhoneBoxKeyboardAddon;

internal static class HotkeyCatalog
{
    internal static readonly string[] ValidModifiers = ["Ctrl", "Alt", "Shift", "Win"];
    private static readonly Dictionary<string, uint> VirtualKeys = BuildKeys();

    internal static bool TryGetVirtualKey(string? key, out uint virtualKey)
    {
        virtualKey = 0;
        return key is not null && VirtualKeys.TryGetValue(key, out virtualKey);
    }

    internal static string DisplayName(string? key)
    {
        if (key is null) return "Tecla não definida";
        if (key.StartsWith("Num", StringComparison.Ordinal) && key.Length == 4 && char.IsDigit(key[3])) return "Numpad " + key[3];
        if (key.StartsWith("Top", StringComparison.Ordinal) && key.Length == 4 && char.IsDigit(key[3])) return key[3].ToString();
        if (key.StartsWith("Num", StringComparison.Ordinal))
        {
            return key switch
            {
                "NumAdd" => "Numpad +", "NumSubtract" => "Numpad -", "NumMultiply" => "Numpad *",
                "NumDivide" => "Numpad /", "NumDecimal" => "Numpad .",
                _ => key
            };
        }
        return key switch
        {
            "Space" => "Espaço", "Enter" => "Enter", "Escape" => "Esc", "Backspace" => "Backspace",
            "Tab" => "Tab", "Insert" => "Insert", "Delete" => "Delete", "Home" => "Home", "End" => "End",
            "PageUp" => "Page Up", "PageDown" => "Page Down", "ArrowUp" => "↑", "ArrowDown" => "↓",
            "ArrowLeft" => "←", "ArrowRight" => "→", "Comma" => ",", "Period" => ".", "Minus" => "-", "Equal" => "=",
            "BracketLeft" => "[", "BracketRight" => "]", "Backslash" => "\\", "Semicolon" => ";", "Quote" => "'",
            "Backquote" => "`", "Slash" => "/", _ => key
        };
    }

    internal static string DisplayCombination(HotkeyBinding binding) => DisplayCombination(binding.Modifiers, binding.Key);

    internal static string DisplayCombination(IEnumerable<string>? modifiers, string? key)
    {
        var prefix = string.Join(" + ", ValidModifiers.Where(m => (modifiers ?? []).Contains(m, StringComparer.OrdinalIgnoreCase)));
        return string.IsNullOrEmpty(prefix) ? DisplayName(key) : prefix + " + " + DisplayName(key);
    }

    internal static bool TryGetModifiers(IEnumerable<string>? modifiers, out uint nativeModifiers, out string? error)
    {
        nativeModifiers = 0;
        error = null;
        var values = (modifiers ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length == 0)
        {
            error = "Escolha pelo menos um modificador: Ctrl, Alt, Shift ou Win.";
            return false;
        }

        foreach (var modifier in values)
        {
            if (string.IsNullOrWhiteSpace(modifier) || !ValidModifiers.Contains(modifier, StringComparer.OrdinalIgnoreCase))
            {
                error = $"Modificador inválido: {modifier ?? "(vazio)"}.";
                return false;
            }
            nativeModifiers |= modifier.ToLowerInvariant() switch
            {
                "ctrl" => 0x0002u,
                "alt" => 0x0001u,
                "shift" => 0x0004u,
                "win" => 0x0008u,
                _ => 0u
            };
        }
        return true;
    }

    private static Dictionary<string, uint> BuildKeys()
    {
        var keys = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i <= 9; i++)
        {
            keys[$"Top{i}"] = (uint)(0x30 + i);
            keys[$"Num{i}"] = (uint)(0x60 + i);
        }
        for (var i = 0; i < 26; i++) keys[((char)('A' + i)).ToString()] = (uint)(0x41 + i);
        for (var i = 1; i <= 24; i++) keys[$"F{i}"] = (uint)(0x70 + i - 1);

        var special = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = 0x20, ["Tab"] = 0x09, ["Enter"] = 0x0D, ["Escape"] = 0x1B,
            ["Backspace"] = 0x08, ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Home"] = 0x24,
            ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
            ["ArrowLeft"] = 0x25, ["ArrowUp"] = 0x26, ["ArrowRight"] = 0x27, ["ArrowDown"] = 0x28,
            ["Minus"] = 0xBD, ["Equal"] = 0xBB, ["BracketLeft"] = 0xDB, ["BracketRight"] = 0xDD,
            ["Backslash"] = 0xDC, ["Semicolon"] = 0xBA, ["Quote"] = 0xDE, ["Backquote"] = 0xC0,
            ["Comma"] = 0xBC, ["Period"] = 0xBE, ["Slash"] = 0xBF,
            ["NumAdd"] = 0x6B, ["NumSubtract"] = 0x6D, ["NumMultiply"] = 0x6A,
            ["NumDivide"] = 0x6F, ["NumDecimal"] = 0x6E
        };
        foreach (var pair in special) keys[pair.Key] = pair.Value;
        return keys;
    }
}
