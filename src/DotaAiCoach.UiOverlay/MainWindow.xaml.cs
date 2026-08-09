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
    private readonly IProfileSession _profileSession;
    private ProfileWindow? _profileWindow;

    public MainWindow(ICoachSession session, IProfileSession profileSession, bool clickThroughEnabled)
    {
        InitializeComponent();
        _clickThroughEnabled = clickThroughEnabled;
        _profileSession = profileSession;
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

    private void OnProfileClick(object sender, RoutedEventArgs e)
    {
        if (_profileWindow is null || !_profileWindow.IsLoaded)
        {
            var detectedSteamId = TryGetDetectedSteamId();
            _profileWindow = new ProfileWindow(_profileSession, detectedSteamId);
        }

        _profileWindow.Show();
        _profileWindow.Activate();
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
