using System.Runtime.InteropServices;

namespace PhoneBoxKeyboardAddon;

internal static class StartupManager
{
    private const string StartupShortcutName = "PhoneBox Keyboard Add-on.lnk";

    internal static string StartupShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupShortcutName);

    internal static void SetEnabled(bool enabled)
    {
        if (enabled) CreateShortcut(StartupShortcutPath, "--background", "Inicia o PhoneBox Keyboard Add-on em segundo plano.");
        else if (File.Exists(StartupShortcutPath)) File.Delete(StartupShortcutPath);
    }

    private static void CreateShortcut(string path, string arguments, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)
            ?? throw new InvalidOperationException("O Windows Script Host não está disponível para criar atalhos.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            dynamic shellObject = shell!;
            shortcut = shellObject.CreateShortcut(path);
            dynamic link = shortcut;
            link.TargetPath = Program.ExecutablePath;
            link.Arguments = arguments;
            link.WorkingDirectory = Path.GetDirectoryName(Program.ExecutablePath)!;
            link.Description = description;
            link.WindowStyle = 7;
            link.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}
