using System.Diagnostics;
using System.Reflection;
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

    internal static bool CanRemovePackageFiles => PackageSource.CanRemovePackageFiles;

    internal static string Schedule(bool removeUserData, bool removePackageFiles)
    {
        if (!CanUninstall(Program.ExecutablePath))
            throw new InvalidOperationException("O complemento não está sendo executado a partir da pasta instalada.");

        string packageDirectory = string.Empty;
        if (removePackageFiles && !PackageSource.TryGetDirectory(out packageDirectory))
            throw new InvalidOperationException("A pasta extraída não foi identificada como um pacote oficial seguro para remoção. Atualize a instalação pelo pacote oficial antes de usar esta opção.");

        var helperId = Guid.NewGuid().ToString("N");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"PhoneBoxKeyboardAddon-uninstall-{Environment.ProcessId}-{helperId}.cmd");
        var installCleanupPath = Path.Combine(Path.GetTempPath(), $"PhoneBoxKeyboardAddon-install-cleanup-{helperId}.ps1");
        var packageCleanupPath = string.Empty;
        using (var installHelperResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhoneBoxKeyboardAddon.InstallCleanupHelper.ps1")
            ?? throw new InvalidOperationException("O helper de remoção da instalação não foi incorporado."))
        using (var installHelperOutput = File.Create(installCleanupPath))
        {
            installHelperResource.CopyTo(installHelperOutput);
        }

        if (removePackageFiles)
        {
            packageCleanupPath = Path.Combine(Path.GetTempPath(), $"PhoneBoxKeyboardAddon-package-cleanup-{helperId}.ps1");
            using var helperResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhoneBoxKeyboardAddon.PackageCleanupHelper.ps1")
                ?? throw new InvalidOperationException("O helper de validação final da pasta do pacote não foi incorporado.");
            using var helperOutput = File.Create(packageCleanupPath);
            helperResource.CopyTo(helperOutput);
        }

        var startup = StartupManager.StartupShortcutPath;
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        // Use UTF-8 for user profile paths that may contain non-ASCII characters.
        script.AppendLine("chcp 65001 >nul");
        script.AppendLine("setlocal DisableDelayedExpansion");
        script.AppendLine($"set \"TARGETPID={Environment.ProcessId}\"");
        script.AppendLine($"set \"INSTALLDIR={EscapeBatchValue(AddonPaths.InstallDirectory)}\"");
        script.AppendLine($"set \"INSTALLCLEANUP={EscapeBatchValue(installCleanupPath)}\"");
        script.AppendLine($"set \"STARTUP={EscapeBatchValue(startup)}\"");
        script.AppendLine($"set \"STARTMENU={EscapeBatchValue(AddonPaths.StartMenuDirectory)}\"");
        script.AppendLine($"set \"DATADIR={EscapeBatchValue(AddonPaths.DataDirectory)}\"");
        script.AppendLine($"set \"LOGROOT={EscapeBatchValue(AddonPaths.LocalAppDataDirectory)}\"");
        script.AppendLine($"set \"PACKAGEDIR={EscapeBatchValue(packageDirectory)}\"");
        script.AppendLine($"set \"PACKAGECLEANUP={EscapeBatchValue(packageCleanupPath)}\"");
        script.AppendLine($"set \"REMOVEUSERDATA={(removeUserData ? "1" : "0")}\"");
        script.AppendLine($"set \"REMOVEPACKAGEFILES={(removePackageFiles ? "1" : "0")}\"");
        script.AppendLine("set \"UNINSTALLLOG=%TEMP%\\PhoneBoxKeyboardAddon-uninstall.log\"");
        script.AppendLine("echo PhoneBox Keyboard Add-on uninstall started > \"%UNINSTALLLOG%\"");
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
        script.AppendLine("if not exist \"%INSTALLCLEANUP%\" echo FAILED: application cleanup helper is missing>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if exist \"%INSTALLCLEANUP%\" powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%INSTALLCLEANUP%\" -InstallDirectory \"%INSTALLDIR%\" >>\"%UNINSTALLLOG%\" 2>&1");
        script.AppendLine("if errorlevel 1 echo FAILED: application cleanup helper returned an error>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if exist \"%INSTALLDIR%\" echo FAILED: application folder remains>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if not exist \"%INSTALLDIR%\" echo Removed application folder>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if not \"%REMOVEUSERDATA%\"==\"1\" goto PRESERVE_USER_DATA");
        script.AppendLine("if exist \"%DATADIR%\" rmdir /s /q \"%DATADIR%\" >nul 2>&1");
        script.AppendLine("if exist \"%DATADIR%\" echo FAILED: settings folder remains>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if not exist \"%DATADIR%\" echo Settings folder removed>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if exist \"%LOGROOT%\" rmdir /s /q \"%LOGROOT%\" >nul 2>&1");
        script.AppendLine("if exist \"%LOGROOT%\" echo FAILED: local logs folder remains>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if not exist \"%LOGROOT%\" echo Local logs folder removed>>\"%UNINSTALLLOG%\"");
        script.AppendLine("goto AFTER_USER_DATA");
        script.AppendLine(":PRESERVE_USER_DATA");
        script.AppendLine("echo User settings and logs preserved>>\"%UNINSTALLLOG%\"");
        script.AppendLine(":AFTER_USER_DATA");
        script.AppendLine("if not \"%REMOVEPACKAGEFILES%\"==\"1\" goto PRESERVE_PACKAGE");
        script.AppendLine("if not exist \"%PACKAGECLEANUP%\" echo FAILED: package cleanup helper is missing>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if exist \"%PACKAGECLEANUP%\" powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%PACKAGECLEANUP%\" -PackageRoot \"%PACKAGEDIR%\" >>\"%UNINSTALLLOG%\" 2>&1");
        script.AppendLine("if exist \"%PACKAGEDIR%\" echo FAILED: extracted package folder remains>>\"%UNINSTALLLOG%\"");
        script.AppendLine("if not exist \"%PACKAGEDIR%\" echo Removed extracted package folder>>\"%UNINSTALLLOG%\"");
        script.AppendLine("goto AFTER_PACKAGE");
        script.AppendLine(":PRESERVE_PACKAGE");
        script.AppendLine("echo Extracted package folder preserved>>\"%UNINSTALLLOG%\"");
        script.AppendLine(":AFTER_PACKAGE");
        script.AppendLine("findstr /I /C:\"FAILED:\" \"%UNINSTALLLOG%\" >nul");
        script.AppendLine("if errorlevel 1 goto UNINSTALL_SUCCESS");
        script.AppendLine("echo Uninstall finished with errors. See this log for details.>>\"%UNINSTALLLOG%\"");
        script.AppendLine("goto UNINSTALL_END");
        script.AppendLine(":UNINSTALL_SUCCESS");
        script.AppendLine("echo Uninstall completed successfully.>>\"%UNINSTALLLOG%\"");
        script.AppendLine(":UNINSTALL_END");
        script.AppendLine("if exist \"%INSTALLCLEANUP%\" del /f /q \"%INSTALLCLEANUP%\" >nul 2>&1");
        script.AppendLine("if exist \"%PACKAGECLEANUP%\" del /f /q \"%PACKAGECLEANUP%\" >nul 2>&1");
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
            AddonLog.Info($"Desinstalação agendada. Remover dados do usuário: {removeUserData}; remover pacote extraído: {removePackageFiles}.");
            return scriptPath;
        }
        catch
        {
            try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
            try { if (File.Exists(installCleanupPath)) File.Delete(installCleanupPath); } catch { }
            try { if (!string.IsNullOrWhiteSpace(packageCleanupPath) && File.Exists(packageCleanupPath)) File.Delete(packageCleanupPath); } catch { }
            throw;
        }
    }

    // Values are assigned and consumed inside double quotes; percent is the only
    // character that still needs escaping while delayed expansion is disabled.
    private static string EscapeBatchValue(string value) => value
        .Replace("%", "%%", StringComparison.Ordinal);
}
