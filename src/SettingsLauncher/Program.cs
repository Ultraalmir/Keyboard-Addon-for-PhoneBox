using System.Runtime.InteropServices;

internal static class Program
{
    private const int ShowNormal = 1;
    private const uint MessageBoxError = 0x00000010;
    private const uint MessageBoxInformation = 0x00000040;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ShellExecuteW", SetLastError = true)]
    private static extern IntPtr ShellExecute(
        IntPtr window,
        string operation,
        string file,
        string parameters,
        string directory,
        int showCommand);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int ShowMessage(IntPtr window, string text, string caption, uint type);

    private static int Main()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                ShowMessage(IntPtr.Zero,
                    "O Windows não informou a pasta LocalAppData deste usuário.",
                    "PhoneBox Keyboard Add-on",
                    MessageBoxError);
                return 2;
            }

            var executable = Path.Combine(
                localAppData,
                "Programs",
                "PhoneBoxKeyboardAddon",
                "PhoneBoxKeyboardAddon.exe");

            if (!File.Exists(executable))
            {
                ShowMessage(IntPtr.Zero,
                    "O PhoneBox Keyboard Add-on ainda não foi instalado.\n\n" +
                    "Extraia o pacote oficial e execute install.ps1 antes de abrir as configurações.",
                    "PhoneBox Keyboard Add-on",
                    MessageBoxInformation);
                return 3;
            }

            var workingDirectory = Path.GetDirectoryName(executable)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var result = ShellExecute(
                IntPtr.Zero,
                "open",
                executable,
                "--settings",
                workingDirectory,
                ShowNormal);

            if (result.ToInt64() <= 32)
            {
                ShowMessage(IntPtr.Zero,
                    $"O Windows não conseguiu abrir o painel de configurações.\nCódigo: {result.ToInt64()}",
                    "PhoneBox Keyboard Add-on",
                    MessageBoxError);
                return 4;
            }

            return 0;
        }
        catch (Exception exception)
        {
            ShowMessage(IntPtr.Zero,
                "Não foi possível abrir as configurações.\n\n" + exception.Message,
                "PhoneBox Keyboard Add-on",
                MessageBoxError);
            return 1;
        }
    }
}
