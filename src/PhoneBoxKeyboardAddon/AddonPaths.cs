namespace PhoneBoxKeyboardAddon;

internal static class AddonPaths
{
    internal static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhoneBoxKeyboardAddon");

    internal static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    internal static string ControlPortFile => Path.Combine(DataDirectory, "control-panel.port");
    internal static string LocalAppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhoneBoxKeyboardAddon");
    internal static string LogDirectory => Path.Combine(LocalAppDataDirectory, "Logs");
    internal static string LogFile => Path.Combine(LogDirectory, "addon.log");
    internal static string InstallDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PhoneBoxKeyboardAddon");
    internal static string InstalledExecutable => Path.Combine(InstallDirectory, "PhoneBoxKeyboardAddon.exe");
    internal static string StartMenuDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "PhoneBox Keyboard Add-on");
}

internal static class ProductInfo
{
    internal const string Name = "PhoneBox Keyboard Add-on";
    internal const string Version = "1.5.1";
}
