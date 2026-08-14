using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DotaAiCoach.Host;
using DotaAiCoach.UiOverlay.Interop;
using DotaAiCoach.UiOverlay.ScreenCapture;
using DotaAiCoach.UiOverlay.ViewModels;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// The overlay window. Click-through is applied after the window handle
/// exists (SourceInitialized), behind the --click-through launch flag —
/// see App.xaml.cs. Left off by default in this slice so a developer can
/// still interact with the window while iterating; the hook itself is
/// real, working code per dota-overlay's requirement, not a stub.
/// </summary>
public partial class MainWindow : Window
{
    private readonly bool _clickThroughEnabled;
    private readonly ICoachSession _session;
    private readonly IProfileSession _profileSession;
    private readonly OverlayViewModel _overlayViewModel;
    private readonly string? _savedSteamIdInput;
    private readonly GlobalHotKey _captureHotKey;
    private ProfileWindow? _profileWindow;

    public MainWindow(ICoachSession session, IProfileSession profileSession, ILlmCoachSession llmSession, bool clickThroughEnabled, string? savedSteamIdInput = null)
    {
        InitializeComponent();
        _clickThroughEnabled = clickThroughEnabled;
        _session = session;
        _profileSession = profileSession;
        _savedSteamIdInput = savedSteamIdInput;
        _overlayViewModel = new OverlayViewModel(session, llmSession, profileSession, Dispatcher);
        DataContext = _overlayViewModel;
        _captureHotKey = new GlobalHotKey(this);
        _captureHotKey.Pressed += async (_, _) => await _overlayViewModel.CaptureAndAskLlmAsync(CancellationToken.None);
        ApplyStartupPosition();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => _captureHotKey.Dispose();
    }

    /// <summary>
    /// Restores the player's last dragged position (see
    /// OnHeaderMouseLeftButtonDown) if one was saved; otherwise leaves
    /// XAML's fixed Left="24" Top="24" in place. A computed "right side of
    /// the work area" default was tried here and reverted — it mixed
    /// System.Windows.Forms.Screen's physical pixels with WPF's DIP-based
    /// Left/Top, landing the window off-screen on any monitor with DPI
    /// scaling above 100%. Not worth re-attempting without a real repro
    /// environment to validate against.
    /// </summary>
    private void ApplyStartupPosition()
    {
        var saved = WindowPositionSettings.Load();
        if (saved is { Left: { } left, Top: { } top })
        {
            Left = left;
            Top = top;
        }
    }

    private void SaveCurrentPosition() => new WindowPositionSettings { Left = Left, Top = Top }.Save();

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;

        if (_clickThroughEnabled)
        {
            WindowStyles.MakeClickThroughAndNonActivating(handle);
        }

        RegisterCaptureHotKey();
    }

    /// <summary>
    /// Registers the persisted (or default Ctrl+Tab) capture hotkey so
    /// the player can trigger a scoreboard capture without alt-tabbing
    /// out of the game to click the overlay button — see
    /// OverlayViewModel.CaptureAndAskLlmAsync for what it runs.
    /// </summary>
    private void RegisterCaptureHotKey()
    {
        var settings = HotKeySettings.Load();
        var registered = _captureHotKey.Register(settings.VirtualKeyCode, (HotKeyModifiers)settings.Modifiers);
        _overlayViewModel.SetHotKeyStatus(registered, settings);
    }

    /// <summary>
    /// Collapses the window down to just its header row (a thin,
    /// draggable strip) instead of hiding it — per explicit request, so
    /// the player can re-expand it with the same button without going
    /// through the tray icon. SizeToContent="Height" on the window
    /// shrinks/grows it automatically as the body's Visibility toggles.
    /// </summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => _overlayViewModel.IsExpanded = !_overlayViewModel.IsExpanded;

    /// <summary>
    /// Lets the player drag the overlay to a new screen position by
    /// grabbing the header row, per explicit request — the window has no
    /// title bar (WindowStyle="None") so there's otherwise no way to
    /// reposition it short of editing MainWindow.xaml's Left/Top. Only
    /// fires when the window can actually receive mouse input (i.e. not
    /// click-through) — DragMove() is a no-op otherwise, which is the
    /// correct behavior, not a bug to work around.
    /// </summary>
    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            SaveCurrentPosition();
        }
    }

    /// <summary>Restores the window from the tray icon's "Show" menu item or a double-click.</summary>
    public void ShowFromTray()
    {
        Show();
        Activate();
    }

    private void OnProfileClick(object sender, RoutedEventArgs e)
    {
        if (_profileWindow is null || !_profileWindow.IsLoaded)
        {
            // Prefer the live GSI-detected Steam ID (most trustworthy —
            // it's the account actually in this match) over the saved
            // one from a previous session; fall back to the saved value
            // only when GSI hasn't reported one yet.
            var steamId = TryGetDetectedSteamId() ?? _savedSteamIdInput;
            _profileWindow = new ProfileWindow(_profileSession, _session, steamId);
        }

        _profileWindow.Show();
        _profileWindow.Activate();
    }

    private async void OnCaptureScreenClick(object sender, RoutedEventArgs e)
    {
        await _overlayViewModel.CaptureAndAskLlmAsync(CancellationToken.None);
    }

    private void OnConfigureRegionClick(object sender, RoutedEventArgs e)
    {
        // Hide this window while picking so it doesn't cover the region
        // the player is about to select on their screen.
        Hide();

        var picker = new RegionPickerWindow();
        var picked = picker.ShowDialog();

        Show();

        if (picked == true && picker.PickedRegion is { } region)
        {
            _overlayViewModel.SetCaptureRegion(region);
        }
    }

    private void OnConfigureHotKeyClick(object sender, RoutedEventArgs e)
    {
        Hide();

        var picker = new HotKeyPickerWindow();
        var picked = picker.ShowDialog();

        Show();

        if (picked == true && picker.PickedVirtualKeyCode is { } vk)
        {
            var settings = new HotKeySettings { VirtualKeyCode = vk, Modifiers = (uint)picker.PickedModifiers };
            settings.Save();
            var registered = _captureHotKey.Register(settings.VirtualKeyCode, (HotKeyModifiers)settings.Modifiers);
            _overlayViewModel.SetHotKeyStatus(registered, settings);
        }
    }

    private string? TryGetDetectedSteamId() => _overlayViewModel.DetectedSteamId;
}
