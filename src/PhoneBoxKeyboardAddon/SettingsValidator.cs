namespace PhoneBoxKeyboardAddon;

internal static class SettingsValidator
{
    internal static List<string> Validate(AddonSettings settings)
    {
        var errors = new List<string>();
        if (!TryNormalizePhoneBoxUrl(settings.PhoneBoxUrl, out _, out var urlError))
            errors.Add(urlError);

        if (settings.Bindings is null || settings.Bindings.Count != 10)
        {
            errors.Add("A configuração precisa conter exatamente dez posições de atalho para páginas.");
            return errors;
        }
        if (settings.Bindings.Any(b => b is null))
        {
            errors.Add("A lista de atalhos contém uma entrada vazia.");
            return errors;
        }

        var pages = settings.Bindings.Select(b => b.Page).ToArray();
        if (!Enumerable.Range(1, 10).All(p => pages.Count(v => v == p) == 1))
            errors.Add("Cada posição de página de 1 a 10 deve aparecer exatamente uma vez.");
        if (settings.NavigationStartPage is < 1 or > 500)
            errors.Add("A posição inicial da navegação deve estar entre 1 e 500.");
        if (settings.NextPage is null || settings.PreviousPage is null)
            errors.Add("As configurações de Próxima página e Página anterior são obrigatórias, mesmo quando desativadas.");

        var combinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in AllEnabledHotkeys(settings))
        {
            if (!HotkeyCatalog.TryGetVirtualKey(item.Key, out _))
                errors.Add($"A tecla configurada para {item.Label} não é compatível.");
            if (!HotkeyCatalog.TryGetModifiers(item.Modifiers, out _, out var modifierError))
                errors.Add($"{item.Label}: {modifierError}");

            var key = string.Join("+", (item.Modifiers ?? []).Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m, StringComparer.OrdinalIgnoreCase)) + "+" + item.Key;
            if (!combinations.Add(key)) errors.Add($"A combinação {HotkeyCatalog.DisplayCombination(item.Modifiers, item.Key)} está repetida.");
        }
        return errors;
    }

    internal static IEnumerable<(string Label, bool Enabled, List<string> Modifiers, string Key)> AllEnabledHotkeys(AddonSettings settings)
    {
        foreach (var binding in settings.Bindings ?? [])
        {
            if (binding.Enabled)
                yield return ($"Página {binding.Page}", true, binding.Modifiers ?? [], binding.Key ?? "");
        }

        if (settings.NextPage is { Enabled: true } next)
            yield return ("Próxima página", true, next.Modifiers ?? [], next.Key ?? "");
        if (settings.PreviousPage is { Enabled: true } previous)
            yield return ("Página anterior", true, previous.Modifiers ?? [], previous.Key ?? "");
    }

    internal static bool TryNormalizePhoneBoxUrl(string? value, out string normalized, out string error)
    {
        normalized = "";
        error = "O endereço do PhoneBox deve ser HTTP local, por exemplo http://127.0.0.1:8765.";
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttp
            || !endpoint.IsLoopback
            || endpoint.Port is < 1 or > 65535
            || endpoint.AbsolutePath != "/"
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !string.IsNullOrEmpty(endpoint.UserInfo))
            return false;

        normalized = endpoint.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        error = "";
        return true;
    }
}
