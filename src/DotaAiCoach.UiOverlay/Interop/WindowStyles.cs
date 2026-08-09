using System.Runtime.InteropServices;

namespace DotaAiCoach.UiOverlay.Interop;

/// <summary>
/// Win32 extended-window-style helpers for making the overlay click-through
/// and non-focus-stealing, per dota-overlay's window-behavior requirements.
/// Click-through is exposed behind an explicit opt-in (see
/// MainWindow.xaml.cs) rather than being always-on in this slice, so a
/// developer iterating on the overlay can still interact with it — the
/// hook exists and is real, functioning code; only the default is off.
/// </summary>
internal static class WindowStyles
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>Makes the window click-through (mouse input passes to whatever is beneath it) and prevents it from stealing focus/activation.</summary>
    public static void MakeClickThroughAndNonActivating(IntPtr hwnd)
    {
        var current = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, current | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>Restores normal input handling (used when the overlay's configuration mode needs to accept clicks/drag).</summary>
    public static void MakeInteractive(IntPtr hwnd)
    {
        var current = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (current & ~WS_EX_TRANSPARENT) | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }
}
