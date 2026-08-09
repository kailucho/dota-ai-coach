using System.Windows;
using DotaAiCoach.Host;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// Composition root: builds the CoachSession (GSI listener bound to
/// 127.0.0.1:3000/gsi by default) via CoachSessionFactory, starts it, and
/// shows the overlay window. Deliberately never references
/// DotaAiCoach.Gsi types directly — only DotaAiCoach.Host's ICoachSession
/// surface — per dota-architecture's UI/GSI separation rule.
/// --click-through enables the click-through/non-activating window
/// style; omitted by default so the window remains interactive during
/// development.
/// </summary>
public partial class App : Application
{
    private ICoachSession? _session;
    private IProfileSession? _profileSession;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var clickThroughEnabled = e.Args.Contains("--click-through");

        _session = CoachSessionFactory.Create();
        await _session.StartAsync(default);

        // No automatic profile fetch at startup — offline-first, the user
        // explicitly refreshes from the profile window (see docs/adr/0012).
        _profileSession = ProfileSessionFactory.Create();

        var window = new MainWindow(_session, _profileSession, clickThroughEnabled);
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_session is not null)
        {
            await _session.StopAsync(default);
            if (_session is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }

        base.OnExit(e);
    }
}
