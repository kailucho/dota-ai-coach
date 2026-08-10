using System.Windows;
using DotaAiCoach.Host;
using Forms = System.Windows.Forms;

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
public partial class App : System.Windows.Application
{
    private ICoachSession? _session;
    private IProfileSession? _profileSession;
    private MainWindow? _window;
    private Forms.NotifyIcon? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The tray icon must survive Hide()-ing the main window, so
        // WPF's default "close the app when the last visible window
        // closes" shutdown mode would tear everything down the moment
        // the user minimizes to tray. Explicit shutdown only, triggered
        // by the tray menu's "Exit" (see BuildTrayIcon below).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var clickThroughEnabled = e.Args.Contains("--click-through");

        _session = CoachSessionFactory.Create();
        await _session.StartAsync(default);

        // No automatic network profile fetch at startup — offline-first,
        // the user explicitly refreshes from the profile window (see
        // docs/adr/0012). The saved Steam ID (per user request: avoid
        // re-typing it every session) DOES let us load the last-cached
        // profile from disk, which is what feeds SetAverageRankTier below
        // — still zero network calls at startup.
        _profileSession = ProfileSessionFactory.Create();

        var settings = LocalPlayerSettings.Load();
        if (!string.IsNullOrWhiteSpace(settings.SteamIdInput))
        {
            var cached = await _profileSession.LoadFromCacheAsync(settings.SteamIdInput, default);
            _session.SetAverageRankTier(cached.RankTier);
        }

        var llmSession = LlmCoachSession.Create();

        _window = new MainWindow(_session, _profileSession, llmSession, clickThroughEnabled, settings.SteamIdInput);
        _window.Show();

        BuildTrayIcon();
    }

    /// <summary>
    /// WPF has no native system-tray control; System.Windows.Forms.
    /// NotifyIcon (via UseWindowsForms in the .csproj) is the standard way
    /// to add one to a WPF app. SystemIcons.Application is used as a
    /// placeholder icon — the project has no custom .ico asset yet.
    /// </summary>
    private void BuildTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => _window?.ShowFromTray());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Dota AI Coach",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => _window?.ShowFromTray();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

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
