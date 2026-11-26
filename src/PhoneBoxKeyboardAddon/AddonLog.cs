using System.Text;

namespace PhoneBoxKeyboardAddon;

internal static class AddonLog
{
    private static readonly object Sync = new();
    private const long MaxBytes = 1_500_000;

    internal static void Info(string message) => Write("INFO", message, null);
    internal static void Warn(string message) => Write("WARN", message, null);
    internal static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AddonPaths.LogDirectory);
                RotateIfNeeded();
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
                if (exception is not null) line += Environment.NewLine + exception;
                File.AppendAllText(AddonPaths.LogFile, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging must never interrupt keyboard handling or startup.
        }
    }

    private static void RotateIfNeeded()
    {
        var current = new FileInfo(AddonPaths.LogFile);
        if (!current.Exists || current.Length < MaxBytes) return;

        var oldest = AddonPaths.LogFile + ".3";
        var second = AddonPaths.LogFile + ".2";
        var first = AddonPaths.LogFile + ".1";
        if (File.Exists(oldest)) File.Delete(oldest);
        if (File.Exists(second)) File.Move(second, oldest);
        if (File.Exists(first)) File.Move(first, second);
        File.Move(AddonPaths.LogFile, first);
    }
}
