using System.Security.Cryptography;
using System.Text.Json;

namespace PhoneBoxKeyboardAddon;

/// <summary>
/// Resolves the exact extracted release-package directory recorded by the installer.
/// A repository checkout or a general user folder is never eligible for cleanup.
/// </summary>
internal static class PackageSource
{
    internal const string MarkerFileName = ".phonebox-keyboard-addon-package";
    internal const string MarkerContent = "PhoneBoxKeyboardAddonPackage:v1";
    internal const string ManifestFileName = "PhoneBoxPackage.manifest";
    internal const string ManifestFormat = "PhoneBoxKeyboardAddonPackageManifest:v1";

    // The settings panel polls status every few seconds. Keep its availability check
    // structural so it does not re-hash the large self-contained executables each time.
    // Full SHA-256 validation still runs when uninstall is requested and again just
    // before the cleanup helper deletes the extracted folder.
    internal static bool CanRemovePackageFiles
    {
        get
        {
            try
            {
                if (!File.Exists(AddonPaths.PackageSourceFile)) return false;
                var recorded = File.ReadAllText(AddonPaths.PackageSourceFile).Trim();
                return IsSafePackageDirectory(recorded, verifyHashes: false);
            }
            catch { return false; }
        }
    }

    internal static bool TryGetDirectory(out string directory)
    {
        directory = string.Empty;
        try
        {
            if (!File.Exists(AddonPaths.PackageSourceFile)) return false;

            var recorded = File.ReadAllText(AddonPaths.PackageSourceFile).Trim();
            if (!IsSafePackageDirectory(recorded, verifyHashes: true)) return false;

            directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(recorded));
            return true;
        }
        catch
        {
            directory = string.Empty;
            return false;
        }
    }

    internal static bool IsSafePackageDirectory(string? candidate) =>
        IsSafePackageDirectory(candidate, verifyHashes: true);

    private static bool IsSafePackageDirectory(string? candidate, bool verifyHashes)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        try
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            var root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath) ?? string.Empty);
            if (string.IsNullOrWhiteSpace(root)
                || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return false;

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var downloads = Path.Combine(userProfile, "Downloads");
            foreach (var protectedFolder in new[] { userProfile, desktop, documents, downloads })
            {
                if (!string.IsNullOrWhiteSpace(protectedFolder)
                    && string.Equals(fullPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedFolder)), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!Directory.Exists(fullPath)) return false;
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0) return false;

            // Never treat operating-system or shared application roots as removable packages.
            var protectedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
            };
            foreach (var protectedRoot in protectedRoots)
            {
                if (string.IsNullOrWhiteSpace(protectedRoot)) continue;
                var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedRoot));
                if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (Directory.Exists(Path.Combine(fullPath, ".git"))
                || Directory.Exists(Path.Combine(fullPath, ".github"))
                || Directory.Exists(Path.Combine(fullPath, "src"))
                || Directory.Exists(Path.Combine(fullPath, "scripts"))
                || Directory.Exists(Path.Combine(fullPath, "docs"))
                || Directory.Exists(Path.Combine(fullPath, "site")))
                return false;

            // Release packages are flat. Reject nested folders and links so recursive
            // cleanup cannot sweep up unrelated data or follow reparse points.
            foreach (var entry in Directory.EnumerateFileSystemEntries(fullPath))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0
                    || (attributes & FileAttributes.Directory) != 0)
                    return false;
            }

            var marker = Path.Combine(fullPath, MarkerFileName);
            if (!File.Exists(marker)
                || (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0
                || !File.ReadAllText(marker).Trim().Equals(MarkerContent, StringComparison.Ordinal))
                return false;

            if (!HasValidManifestAndExactContents(fullPath, verifyHashes)) return false;

            var installDirectory = Path.GetFullPath(AddonPaths.InstallDirectory);
            var separator = Path.DirectorySeparatorChar;
            if (installDirectory.StartsWith(fullPath + separator, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(installDirectory + separator, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullPath, installDirectory, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasValidManifestAndExactContents(string directory, bool verifyHashes)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath)
            || (File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0)
            return false;

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("format", out var format)
            || format.GetString() != ManifestFormat
            || !root.TryGetProperty("files", out var files)
            || files.ValueKind != JsonValueKind.Array)
            return false;

        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasApplication = false;
        var hasLauncher = false;
        var hasInstaller = false;
        var hasUninstaller = false;
        var hasReadme = false;
        var hasLicense = false;

        foreach (var item in files.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("name", out var nameElement)
                || !item.TryGetProperty("sha256", out var hashElement))
                return false;

            var name = nameElement.GetString();
            var expectedHash = hashElement.GetString();
            if (string.IsNullOrWhiteSpace(name)
                || !string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal)
                || name is "." or ".."
                || name.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(MarkerFileName, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(expectedHash)
                || expectedHash.Length != 64
                || expectedHash.Any(character => !Uri.IsHexDigit(character))
                || !expectedNames.Add(name))
                return false;

            var filePath = Path.Combine(directory, name);
            if (!File.Exists(filePath)
                || (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
                return false;

            if (verifyHashes)
            {
                using var stream = File.OpenRead(filePath);
                var actualHash = Convert.ToHexString(SHA256.HashData(stream));
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) return false;
            }

            hasApplication |= name.Equals("PhoneBoxKeyboardAddon.exe", StringComparison.OrdinalIgnoreCase);
            hasLauncher |= name.Equals("Abrir-Configuracoes.exe", StringComparison.OrdinalIgnoreCase);
            hasInstaller |= name.Equals("install.ps1", StringComparison.OrdinalIgnoreCase);
            hasUninstaller |= name.Equals("uninstall.ps1", StringComparison.OrdinalIgnoreCase);
            hasReadme |= name.Equals("README.md", StringComparison.OrdinalIgnoreCase);
            hasLicense |= name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase);
        }

        if (!hasApplication || !hasLauncher || !hasInstaller || !hasUninstaller || !hasReadme || !hasLicense)
            return false;

        var actualNames = Directory.EnumerateFiles(directory)
            .Select(filePath => Path.GetFileName(filePath)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredNames = new HashSet<string>(expectedNames, StringComparer.OrdinalIgnoreCase)
        {
            ManifestFileName,
            MarkerFileName
        };
        return actualNames.SetEquals(requiredNames);
    }
}
