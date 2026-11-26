using System.Diagnostics;
using System.Text;

namespace PhoneBoxKeyboardAddon;

/// <summary>Schedules a per-user self-uninstall after the running executable has exited.</summary>
internal static class UninstallScheduler
{
    internal static bool CanUninstall(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        try
        {
            var actual = Path.GetFullPath(executablePath);
            var installed = Path.GetFullPath(AddonPaths.InstalledExecutable);
            return string.Equals(actual, installed, StringComparison.OrdinalIgnoreCase)
                && File.Exists(installed);
        }
        catch { return false; }
    }

    internal static string Schedule(bool removeUserData)
    {
        if (!CanUninstall(Program.ExecutablePath))
            throw new InvalidOperationException("O complemento não está sendo executado a partir da pasta instalada.");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"PhoneBoxKeyboardAddon-uninstall-{Environment.ProcessId}-{Guid.NewGuid():N}.cmd");
        var startup = StartupManager.StartupShortcutPath;
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        // Use UTF-8 for user profile paths that may contain non-ASCII characters.
        script.AppendLine("chcp 65001 >nul");
        script.AppendLine("setlocal DisableDelayedExpansion");
        script.AppendLine($"set \"TARGETPID={Environment.ProcessId}\"");
        script.AppendLine($"set \"INSTALLDIR={EscapeBatchValue(AddonPaths.InstallDirectory)}\"");
        script.AppendLine($"set \"STARTUP={EscapeBatchValue(startup)}\"");
        script.AppendLine($"set \"STARTMENU={EscapeBatchValue(AddonPaths.StartMenuDirectory)}\"");
        script.AppendLine($"set \"DATADIR={EscapeBatchValue(AddonPaths.DataDirectory)}\"");
        script.AppendLine($"set \"LOGDIR={EscapeBatchValue(AddonPaths.LogDirectory)}\"");
        script.AppendLine($"set \"REMOVEUSERDATA={(removeUserData ? "1" : "0")}\"");
        script.AppendLine("set /a WAITCOUNT=0");
        script.AppendLine(":WAIT_FOR_ADDON");
        script.AppendLine("tasklist /FI \"PID eq %TARGETPID%\" /FI \"IMAGENAME eq PhoneBoxKeyboardAddon.exe\" /NH | findstr /I /C:\"PhoneBoxKeyboardAddon.exe\" >nul");
        script.AppendLine("if errorlevel 1 goto CLEANUP");
        script.AppendLine("set /a WAITCOUNT+=1");
        script.AppendLine("if %WAITCOUNT% GEQ 90 taskkill /PID %TARGETPID% /T /F >nul 2>&1");
        script.AppendLine("timeout /t 1 /nobreak >nul");
        script.AppendLine("goto WAIT_FOR_ADDON");
        script.AppendLine(":CLEANUP");
        script.AppendLine("if exist \"%STARTUP%\" del /f /q \"%STARTUP%\" >nul 2>&1");
        script.AppendLine("if exist \"%STARTMENU%\" rmdir /s /q \"%STARTMENU%\" >nul 2>&1");
        script.AppendLine("if exist \"%INSTALLDIR%\" rmdir /s /q \"%INSTALLDIR%\" >nul 2>&1");
        script.AppendLine("if \"%REMOVEUSERDATA%\"==\"1\" (");
        script.AppendLine("  if exist \"%DATADIR%\" rmdir /s /q \"%DATADIR%\" >nul 2>&1");
        script.AppendLine("  if exist \"%LOGDIR%\" rmdir /s /q \"%LOGDIR%\" >nul 2>&1");
        script.AppendLine(")");
        script.AppendLine("del /f /q \"%~f0\" >nul 2>&1");
        script.AppendLine("endlocal");
        File.WriteAllText(scriptPath, script.ToString(), new UTF8Encoding(false));

        var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var startInfo = new ProcessStartInfo
        {
            FileName = commandProcessor,
            Arguments = $"/d /c \"\"{scriptPath}\"\"",
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        try
        {
            using (Process.Start(startInfo)
                ?? throw new InvalidOperationException("O Windows não iniciou o processo de desinstalação."))
            {
                // Disposing the Process handle does not stop the launched cleanup process.
            }
            AddonLog.Info($"Desinstalação agendada. Remover dados do usuário: {removeUserData}.");
            return scriptPath;
        }
        catch
        {
            try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
            throw;
        }
    }

    private static string EscapeBatchValue(string value) => value.Replace("%", "%%", StringComparison.Ordinal).Replace("^", "^^", StringComparison.Ordinal);
}
