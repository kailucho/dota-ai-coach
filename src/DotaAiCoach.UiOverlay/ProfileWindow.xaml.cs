using System.Windows;
using DotaAiCoach.Host;
using DotaAiCoach.UiOverlay.ViewModels;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// A separate, normal (non-transparent, resizable) window for the Fase 2
/// player profile dashboard — deliberately not built as navigation inside
/// the live overlay window, per the scope decision in docs/adr/0012.
/// </summary>
public partial class ProfileWindow : Window
{
    private readonly ProfileViewModel _viewModel;

    public ProfileWindow(IProfileSession session, string? detectedSteamId)
    {
        InitializeComponent();
        _viewModel = new ProfileViewModel(session, Dispatcher, detectedSteamId);
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            // Offline-first: load from cache immediately, never an
            // automatic network fetch — the user explicitly refreshes.
            if (!string.IsNullOrWhiteSpace(_viewModel.SteamIdInput))
            {
                await session.LoadFromCacheAsync(_viewModel.SteamIdInput, CancellationToken.None);
            }
        };
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
    }
}
