using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhoneBoxKeyboardAddon;

/// <summary>
/// Invisible message-only window that owns all RegisterHotKey/UnregisterHotKey
/// calls. Windows hotkey registration is kept on the same thread that owns this HWND.
/// </summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmShutdown = 0x8001;
    private const int WmDispatch = 0x8002;
    private readonly HotkeyManager _manager;
    private readonly Action _shutdown;
    private readonly int _ownerThreadId;
    private readonly ConcurrentQueue<Action> _workQueue = new();
    private readonly object _lifecycleSync = new();
    private volatile bool _disposed;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    internal HotkeyWindow(HotkeyManager manager, Action shutdown)
    {
        _manager = manager;
        _shutdown = shutdown;
        _ownerThreadId = Environment.CurrentManagedThreadId;
        var parameters = new CreateParams
        {
            Caption = "PhoneBoxKeyboardAddon.MessageWindow",
            Parent = new IntPtr(-3) // HWND_MESSAGE: invisible message-only window.
        };
        CreateHandle(parameters);
    }

    /// <summary>Runs a synchronous callback on the HWND-owning UI thread.</summary>
    internal Task<T> InvokeAsync<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HotkeyWindow));
            return Task.FromResult(callback());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lifecycleSync)
        {
            if (_disposed)
                return Task.FromException<T>(new ObjectDisposedException(nameof(HotkeyWindow)));

            _workQueue.Enqueue(() =>
            {
                if (completion.Task.IsCompleted) return;
                if (_disposed)
                {
                    completion.TrySetException(new ObjectDisposedException(nameof(HotkeyWindow)));
                    return;
                }

                try { completion.TrySetResult(callback()); }
                catch (Exception ex) { completion.TrySetException(ex); }
            });

            if (!PostMessage(Handle, WmDispatch, IntPtr.Zero, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                completion.TrySetException(new Win32Exception(error, "Não foi possível enviar uma operação à thread de hotkeys."));
            }
        }

        return completion.Task;
    }

    internal void RequestShutdown()
    {
        lock (_lifecycleSync)
        {
            if (_disposed) return;
            if (!PostMessage(Handle, WmShutdown, IntPtr.Zero, IntPtr.Zero))
                AddonLog.Warn($"Não foi possível solicitar o encerramento da janela de hotkeys (erro {Marshal.GetLastWin32Error()}).");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            _manager.HandleHotkey(m.WParam.ToInt32());
            return;
        }
        if (m.Msg == WmDispatch)
        {
            while (_workQueue.TryDequeue(out var work)) work();
            return;
        }
        if (m.Msg == WmShutdown)
        {
            _shutdown();
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("A janela de hotkeys deve ser encerrada na thread que a criou.");

        lock (_lifecycleSync)
        {
            if (_disposed) return;
            _disposed = true;
            while (_workQueue.TryDequeue(out var work)) work();
            DestroyHandle();
        }
    }
}
