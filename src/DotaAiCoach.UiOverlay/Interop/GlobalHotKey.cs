using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DotaAiCoach.UiOverlay.Interop;

[Flags]
public enum HotKeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,

    /// <summary>Suppresses repeated WM_HOTKEY while the key is held — always included when registering.</summary>
    NoRepeat = 0x4000,
}

/// <summary>
/// Wraps RegisterHotKey/UnregisterHotKey + a WM_HOTKEY HwndSource hook, so
/// the player can trigger the scoreboard capture with a system-wide key
/// combo instead of clicking the overlay's button (e.g. right after
/// pressing Dota's own Tab-to-open-scoreboard bind) — see
/// OverlayViewModel.CaptureAndAskLlmAsync, the same pipeline the
/// "Capturar pantalla" button already runs. Deliberately never registers a
/// bare, unmodified key: Windows intercepts a globally-registered key at
/// the OS level before it reaches the foreground app, so a bare Tab here
/// would silently break Dota's own Tab-opens-scoreboard bind while this
/// app is running (see HotKeySettings for the default-combo rationale).
/// </summary>
internal sealed class GlobalHotKey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotKeyId = 0x4A21;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Window _window;
    private HwndSource? _hwndSource;
    private bool _isRegistered;

    public event EventHandler? Pressed;

    public GlobalHotKey(Window window)
    {
        _window = window;
    }

    /// <summary>Registers (replacing any previous registration) the given virtual-key + modifiers combo. Returns false if the combo is unavailable (owned by another app/the OS).</summary>
    public bool Register(int virtualKeyCode, HotKeyModifiers modifiers)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            // Window handle doesn't exist yet — caller must retry from SourceInitialized onward.
            return false;
        }

        if (_hwndSource is null)
        {
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(WndProc);
        }

        Unregister();

        _isRegistered = RegisterHotKey(handle, HotKeyId, (uint)(modifiers | HotKeyModifiers.NoRepeat), (uint)virtualKeyCode);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotKeyId);
        }

        _isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _hwndSource?.RemoveHook(WndProc);
    }
}
