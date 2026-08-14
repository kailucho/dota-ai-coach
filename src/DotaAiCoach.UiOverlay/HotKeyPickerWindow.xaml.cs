using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DotaAiCoach.UiOverlay.Interop;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// Small modal dialog for reconfiguring the scoreboard-capture hotkey —
/// mirrors RegionPickerWindow's "do one focused capture, then close"
/// shape. Any key or combination is allowed, including a bare key with no
/// modifier — the player explicitly asked for this after finding the
/// modifier requirement too restrictive. A bare key IS globally
/// intercepted by Windows while registered (see HotKeySettings' Ctrl+Tab
/// default note) — e.g. picking bare Tab would stop Dota's own Tab-opens-
/// scoreboard bind from reaching the game — so a warning is shown for
/// that case rather than a hard block, leaving the choice to the player.
/// </summary>
public partial class HotKeyPickerWindow : Window, INotifyPropertyChanged
{
    private string _capturedText = "...";
    private string? _errorText;
    private string? _warningText;
    private bool _accepted;

    public int? PickedVirtualKeyCode { get; private set; }
    public HotKeyModifiers PickedModifiers { get; private set; }

    public string CapturedText { get => _capturedText; private set { _capturedText = value; OnPropertyChanged(); } }
    public string? ErrorText { get => _errorText; private set { _errorText = value; OnPropertyChanged(); } }
    public string? WarningText { get => _warningText; private set { _warningText = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    public HotKeyPickerWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (_accepted)
        {
            // Already captured a combo and waiting out the confirmation
            // delay before closing -- ignore further key presses.
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }

        // Ignore a lone modifier press -- wait for the actual key (the
        // modifier, if any, held down while a non-modifier key is
        // pressed), or the modifier being released on its own counts as
        // nothing captured yet.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = ToHotKeyModifiers(Keyboard.Modifiers);
        var vk = KeyInterop.VirtualKeyFromKey(key);

        // Verify the combo is actually registrable (not owned by the OS
        // or another app) before accepting it -- e.g. Ctrl+Alt+Del can
        // never be delivered to RegisterHotKey. This window itself has a
        // real HWND by the time PreviewKeyDown fires (ShowDialog already
        // created it), so it can host the probe registration directly.
        using var probe = new GlobalHotKey(this);
        if (!probe.Register(vk, modifiers))
        {
            ErrorText = "Esa combinacion no esta disponible (la usa otra aplicacion o el sistema). Prueba otra.";
            WarningText = null;
            CapturedText = "...";
            return;
        }

        probe.Unregister();

        _accepted = true;
        PickedVirtualKeyCode = vk;
        PickedModifiers = modifiers;
        CapturedText = FormatCombo(modifiers, key);
        ErrorText = null;
        WarningText = modifiers == HotKeyModifiers.None
            ? "Sin modificador: esta tecla dejara de llegarle a otras apps (incluido Dota) mientras esta activa."
            : null;

        // Let the player actually see what was captured before the dialog
        // closes -- closing immediately made it look like nothing had
        // registered.
        await Dispatcher.Yield(DispatcherPriority.Render);
        await Task.Delay(700);

        DialogResult = true;
        Close();
    }

    private static HotKeyModifiers ToHotKeyModifiers(ModifierKeys modifiers)
    {
        var result = HotKeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotKeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotKeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotKeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotKeyModifiers.Win;
        return result;
    }

    private static string FormatCombo(HotKeyModifiers modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotKeyModifiers.Win)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
