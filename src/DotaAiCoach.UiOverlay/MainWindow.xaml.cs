using System.Windows;
using System.Windows.Interop;
using DotaAiCoach.Host;
using DotaAiCoach.UiOverlay.Interop;
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
    private readonly ILlmCoachSession _llmSession;
    private readonly string? _savedSteamIdInput;
    private ProfileWindow? _profileWindow;
    private AskLlmWindow? _askLlmWindow;

    public MainWindow(ICoachSession session, IProfileSession profileSession, ILlmCoachSession llmSession, bool clickThroughEnabled, string? savedSteamIdInput = null)
    {
        InitializeComponent();
        _clickThroughEnabled = clickThroughEnabled;
        _session = session;
        _profileSession = profileSession;
        _llmSession = llmSession;
        _savedSteamIdInput = savedSteamIdInput;
        DataContext = new OverlayViewModel(session, Dispatcher);
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (!_clickThroughEnabled)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeClickThroughAndNonActivating(handle);
    }

    /// <summary>
    /// Hides the window entirely rather than a normal Windows minimize —
    /// this window already runs with ShowInTaskbar=False (an overlay has
    /// no taskbar presence by design, see dota-overlay), so a standard
    /// WindowState.Minimized would vanish with no way back short of
    /// killing the process. The system tray icon (see App.xaml.cs) is
    /// what lets the user bring it back.
    /// </summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => Hide();

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

    private void OnAskLlmClick(object sender, RoutedEventArgs e)
    {
        if (_askLlmWindow is null || !_askLlmWindow.IsLoaded)
        {
            _askLlmWindow = new AskLlmWindow(_llmSession);
        }

        _askLlmWindow.Show();
        _askLlmWindow.Activate();
    }

    private string? TryGetDetectedSteamId()
    {
        if (DataContext is OverlayViewModel viewModel)
        {
            return viewModel.DetectedSteamId;
        }

        return null;
    }
}
