using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AnyVoice.Desktop;

public sealed class GlobalHotkeyController : IDisposable
{
    private const int HotkeyId = 0xA11;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyV = 0x56;

    private readonly Window window;
    private HwndSource? source;
    private bool desiredEnabled;
    private bool registered;
    private bool disposed;

    public GlobalHotkeyController(Window window)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        window.SourceInitialized += Window_SourceInitialized;
        window.Closed += Window_Closed;
    }

    public event EventHandler? Triggered;

    public event EventHandler? RegistrationFailed;

    public void UpdateEnabled(bool enabled)
    {
        desiredEnabled = enabled;
        ApplyRegistration();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        desiredEnabled = false;
        ApplyRegistration();
        if (source is not null)
        {
            source.RemoveHook(WndProc);
            source = null;
        }

        window.SourceInitialized -= Window_SourceInitialized;
        window.Closed -= Window_Closed;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
        ApplyRegistration();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private void ApplyRegistration()
    {
        var handle = source?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (!desiredEnabled && registered)
        {
            _ = UnregisterHotKey(handle, HotkeyId);
            registered = false;
            return;
        }

        if (desiredEnabled && !registered)
        {
            registered = RegisterHotKey(
                handle,
                HotkeyId,
                ModControl | ModAlt | ModNoRepeat,
                VirtualKeyV);
            if (!registered)
            {
                desiredEnabled = false;
                RegistrationFailed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
