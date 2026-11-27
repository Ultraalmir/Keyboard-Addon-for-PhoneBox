using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PhoneBoxKeyboardAddon;

internal sealed class UpdateManager
{
    private const string Repository = "Ultraalmir/Keyboard-Addon-for-PhoneBox";
    private const string PackageName = "PhoneBoxKeyboardAddon-win-x64.zip";
    private const string ChecksumsName = "SHA256SUMS.txt";
    private static readonly HttpClient Http = CreateHttpClient();

    internal async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repository}/releases/latest");
            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = json.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var releaseUrl = root.GetProperty("html_url").GetString() ?? $"https://github.com/{Repository}/releases";
            if (!IsTrustedReleaseUrl(releaseUrl))
                releaseUrl = $"https://github.com/{Repository}/releases";
            var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
            var prerelease = root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
            if (prerelease) return new(true, ProductInfo.Version, ProductInfo.Version, false, releaseUrl, notes, null, IsInstalledInstance());
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)
                || !Version.TryParse(ProductInfo.Version, out var current))
                return new(false, ProductInfo.Version, "", false, releaseUrl, "", "A versão publicada possui uma identificação inválida.", false);

            string? packageUrl = null, checksumUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    var url = asset.GetProperty("browser_download_url").GetString();
                    if (name == PackageName) packageUrl = url;
                    if (name == ChecksumsName) checksumUrl = url;
                }
            }

            var available = latest > current;
            if (available && (string.IsNullOrWhiteSpace(packageUrl) || string.IsNullOrWhiteSpace(checksumUrl)
                || !IsTrustedAssetUrl(packageUrl) || !IsTrustedAssetUrl(checksumUrl)))
                return new(false, ProductInfo.Version, tag, false, releaseUrl, notes, "A release não contém os assets oficiais esperados.", false);

            return new(true, ProductInfo.Version, tag, available, releaseUrl, notes, null, IsInstalledInstance(), packageUrl, checksumUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            AddonLog.Warn($"Não foi possível verificar atualizações: {ex.Message}");
            return new(false, ProductInfo.Version, "", false,
                $"https://github.com/{Repository}/releases", "", "Não foi possível consultar as versões no GitHub.", false);
        }
    }

    internal async Task<(bool Success, string Message)> PrepareAndLaunchAsync()
    {
        var check = await CheckAsync();
        if (!check.Success) return (false, check.Error ?? "Não foi possível verificar a atualização.");
        if (!check.UpdateAvailable) return (false, "O complemento já está na versão mais recente.");
        if (!check.CanAutoUpdate)
            return (false, "A atualização automática está disponível somente para a instalação no Menu Iniciar. Execute o instalador da release para instalar esta cópia.");

        var tempDir = Path.Combine(Path.GetTempPath(), "PhoneBoxKeyboardAddon-Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var packagePath = Path.Combine(tempDir, PackageName);
            var checksumPath = Path.Combine(tempDir, ChecksumsName);
            await DownloadAsync(check.PackageUrl!, packagePath);
            await DownloadAsync(check.ChecksumUrl!, checksumPath);

            var expectedHash = ReadExpectedHash(await File.ReadAllTextAsync(checksumPath));
            await using (var file = File.OpenRead(packagePath))
            {
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(expectedHash),
                    System.Text.Encoding.ASCII.GetBytes(actualHash)))
                    throw new InvalidDataException("O SHA-256 do pacote não corresponde ao checksum publicado.");
            }

            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var exe = archive.Entries.SingleOrDefault(e => e.FullName.Equals("PhoneBoxKeyboardAddon.exe", StringComparison.Ordinal));
                if (exe is null || exe.Length < 100_000)
                    throw new InvalidDataException("O pacote não contém o executável esperado.");
            }

            var helper = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhoneBoxKeyboardAddon.UpdateHelper.ps1")
                ?? throw new InvalidOperationException("O helper de atualização não foi incorporado.");
            var helperPath = Path.Combine(tempDir, "UpdateHelper.ps1");
            await using (var output = File.Create(helperPath)) await helper.CopyToAsync(output);

            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(powershell)) powershell = "powershell.exe";
            var start = new ProcessStartInfo(powershell)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempDir
            };
            foreach (var argument in new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", helperPath,
                "-PackagePath", packagePath, "-InstallDirectory", AddonPaths.InstallDirectory,
                "-ParentPid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-Version", check.LatestVersion, "-ExpectedSha256", expectedHash
            })
                start.ArgumentList.Add(argument);
            if (Process.Start(start) is null)
                throw new InvalidOperationException("Não foi possível iniciar o atualizador.");
            AddonLog.Info($"Atualização {check.LatestVersion} preparada; helper iniciado.");
            return (true, "Atualização preparada. O complemento será fechado, atualizado e reiniciado automaticamente.");
        }
        catch (Exception ex)
        {
            AddonLog.Error("Falha ao preparar a atualização automática.", ex);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
            return (false, ex.Message);
        }
    }

    private static bool IsTrustedReleaseUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith($"/{Repository}/releases/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrustedAssetUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInstalledInstance()
    {
        try
        {
            return string.Equals(Path.GetFullPath(Program.ExecutablePath),
                Path.GetFullPath(AddonPaths.InstalledExecutable), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static async Task DownloadAsync(string url, string path)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target);
    }

    internal static string ReadExpectedHash(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[^1].TrimStart('*').Equals(PackageName, StringComparison.OrdinalIgnoreCase)
                && parts[0].Length == 64 && parts[0].All(Uri.IsHexDigit))
                return parts[0].ToLowerInvariant();
        }
        throw new InvalidDataException("O arquivo SHA256SUMS.txt não contém o checksum do pacote esperado.");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PhoneBoxKeyboardAddon", ProductInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}

internal sealed record UpdateCheckResult(
    bool Success, string CurrentVersion, string LatestVersion, bool UpdateAvailable,
    string ReleaseUrl, string Notes, string? Error, bool CanAutoUpdate,
    string? PackageUrl = null, string? ChecksumUrl = null);
