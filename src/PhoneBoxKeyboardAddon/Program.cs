using System.Diagnostics;
using System.Windows.Forms;

namespace PhoneBoxKeyboardAddon;

internal static class Program
{
    internal const int DefaultSettingsPort = 47831;
    internal static int SettingsPort
    {
        get
        {
            try
            {
                var text = File.ReadAllText(AddonPaths.ControlPortFile).Trim();
                if (int.TryParse(text, out var port) && port is >= DefaultSettingsPort and <= DefaultSettingsPort + 20)
                    return port;
            }
            catch { }
            return DefaultSettingsPort;
        }
    }
    internal static string ExecutablePath => Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Não foi possível identificar o executável.");

    private const string MutexName = @"Local\PhoneBoxKeyboardAddon.v1";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            var failures = SelfTests.Run();
            Environment.ExitCode = failures == 0 ? 0 : 1;
            return;
        }

        var startInBackground = args.Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));
        var openSettings = !startInBackground && args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase));
        Mutex? mutex = null;
        var ownsMutex = false;
        try
        {
            mutex = new Mutex(true, MutexName, out ownsMutex);
            if (!ownsMutex)
            {
                if (openSettings) OpenSettingsPage();
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => AddonLog.Error("Erro não tratado na thread da interface.", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AddonLog.Error("Erro não tratado no processo.", e.ExceptionObject as Exception);

            AddonLog.Info($"Inicializando PhoneBox Keyboard Add-on {ProductInfo.Version}{(startInBackground ? " em segundo plano" : string.Empty)}.");
            using var context = new AddonApplicationContext(openSettings);
            Application.Run(context);
            AddonLog.Info("PhoneBox Keyboard Add-on encerrado.");
        }
        catch (Exception ex)
        {
            AddonLog.Error("Falha ao iniciar o complemento.", ex);
            if (openSettings)
            {
                MessageBox.Show(
                    $"Não foi possível iniciar o PhoneBox Keyboard Add-on.\n\n{ex.Message}\n\nConsulte o log em {AddonPaths.LogFile}",
                    "PhoneBox Keyboard Add-on", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (ownsMutex)
            {
                try { mutex?.ReleaseMutex(); } catch (ApplicationException) { }
            }
            mutex?.Dispose();
        }
    }

    internal static void OpenSettingsPage()
    {
        var url = $"http://127.0.0.1:{SettingsPort}/";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddonLog.Error("Não foi possível abrir a página de configurações.", ex);
        }
    }
}
