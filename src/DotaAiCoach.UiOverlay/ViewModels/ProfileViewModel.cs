using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using DotaAiCoach.Host;
using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Model;
using DotaAiCoach.PlayerProfile.Scoring;

namespace DotaAiCoach.UiOverlay.ViewModels;

/// <summary>
/// Formats and binds IProfileSession's PlayerProfile-only output for
/// display. Zero scoring logic — every value here is a direct format of a
/// PlayerProfile field, same discipline as OverlayViewModel.
/// </summary>
public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private readonly IProfileSession _session;
    private readonly ICoachSession _coachSession;
    private readonly Dispatcher _dispatcher;

    public ProfileViewModel(IProfileSession session, ICoachSession coachSession, Dispatcher dispatcher, string? detectedSteamId)
    {
        _session = session;
        _coachSession = coachSession;
        _dispatcher = dispatcher;
        _session.Updated += OnUpdated;
        SteamIdInput = detectedSteamId ?? "";
        HeroComfortScores = new ObservableCollection<HeroComfortScoreDisplay>();
        DnaDimensions = new ObservableCollection<DnaDimensionDisplay>();
        Apply(_session.Current);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SteamIdInput { get; set; }
    public string PersonaNameText { get; private set; } = "-";
    public string AvailabilityText { get; private set; } = "No profile loaded yet.";
    public string RankText { get; private set; } = "-";
    public string RecordText { get; private set; } = "-";
    public bool IsRefreshing { get; private set; }
    public bool IsDeepRefreshing { get; private set; }
    public string DeepRefreshStatusText { get; private set; } = "";
    public ObservableCollection<HeroComfortScoreDisplay> HeroComfortScores { get; }
    public ObservableCollection<DnaDimensionDisplay> DnaDimensions { get; }

    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        RaiseAllPropertiesChanged();

        var snapshot = await _session.RefreshAsync(SteamIdInput, CancellationToken.None);

        IsRefreshing = false;
        Apply(snapshot);

        SaveSteamIdAndPropagateRank(snapshot);
    }

    /// <summary>
    /// Persists the Steam ID locally (per user request: avoid re-typing
    /// it every session) and feeds the freshly-fetched rank tier into the
    /// live win-probability rule via CoachSession — only on an explicit,
    /// successful refresh, never automatically, matching this window's
    /// existing offline-first discipline.
    /// </summary>
    private void SaveSteamIdAndPropagateRank(PlayerProfileSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(SteamIdInput))
        {
            new LocalPlayerSettings { SteamIdInput = SteamIdInput }.Save();
        }

        _coachSession.SetAverageRankTier(snapshot.RankTier);
    }

    /// <summary>
    /// Parses up to 20 of the player's most recent ranked matches to
    /// unlock the six parsed-data DNA dimensions — expensive, so only ever
    /// runs when the user explicitly clicks the Deep DNA Refresh button
    /// (see docs/adr/0016), never automatically.
    /// </summary>
    public async Task RequestDeepDnaRefreshAsync()
    {
        IsDeepRefreshing = true;
        DeepRefreshStatusText = "Starting…";
        RaiseAllPropertiesChanged();

        var progress = new Progress<DeepDnaRefreshProgress>(p =>
        {
            DeepRefreshStatusText = $"Parsing match {p.Completed}/{p.Total}…";
            RaiseAllPropertiesChanged();
        });

        var snapshot = await _session.RequestDeepDnaRefreshAsync(SteamIdInput, maxMatches: 20, progress, CancellationToken.None);

        IsDeepRefreshing = false;
        DeepRefreshStatusText = "";
        Apply(snapshot);
    }

    private void OnUpdated(object? sender, PlayerProfileSnapshot snapshot)
    {
        if (_dispatcher.CheckAccess())
        {
            Apply(snapshot);
        }
        else
        {
            _dispatcher.Invoke(() => Apply(snapshot));
        }
    }

    private void Apply(PlayerProfileSnapshot snapshot)
    {
        PersonaNameText = snapshot.PersonaName ?? "-";
        AvailabilityText = FormatAvailability(snapshot.Availability);
        RankText = snapshot.RankTier?.ToString() ?? "-";
        RecordText = snapshot.RankedWins is int w && snapshot.RankedLosses is int l ? $"{w}W / {l}L" : "-";

        HeroComfortScores.Clear();
        foreach (var hero in snapshot.Heroes.OrderByDescending(h => h.Games ?? 0))
        {
            var scorer = new HeroComfortScorer();
            var overallGames = (snapshot.RankedWins ?? 0) + (snapshot.RankedLosses ?? 0);
            var score = scorer.Score(snapshot.Availability, hero.HeroId, hero,
                overallGames, snapshot.RankedWins ?? 0,
                snapshot.RecentMatches.Where(m => m.HeroId == hero.HeroId).ToArray());

            HeroComfortScores.Add(new HeroComfortScoreDisplay(
                hero.HeroId,
                score.Score?.ToString("F0") ?? "-",
                hero.Games?.ToString() ?? "0"));
        }

        DnaDimensions.Clear();
        if (snapshot.Dna is not null)
        {
            foreach (var dimension in snapshot.Dna.Dimensions)
            {
                DnaDimensions.Add(new DnaDimensionDisplay(
                    dimension.Dimension.ToString(),
                    dimension.Score?.ToString("F0") ?? "-",
                    FormatDnaAvailability(dimension.Availability)));
            }
        }

        RaiseAllPropertiesChanged();
    }

    private static string FormatAvailability(ProfileAvailability availability) => availability switch
    {
        ProfileAvailability.Unknown => "No profile loaded yet.",
        ProfileAvailability.Available => "Loaded.",
        ProfileAvailability.MatchHistoryPrivate =>
            "Match history is private. Enable 'Expose Public Match Data' in Dota 2 -> Settings -> Options -> Advanced, then refresh.",
        ProfileAvailability.PlayerNotFound => "No OpenDota profile found for this Steam ID.",
        ProfileAvailability.FetchFailed => "Could not reach OpenDota and no cached data is available.",
        ProfileAvailability.CacheOnlyStale => "Showing cached data — could not reach OpenDota just now.",
        _ => "Unknown status.",
    };

    private static string FormatDnaAvailability(DnaAvailability availability) => availability switch
    {
        DnaAvailability.Computed => "",
        DnaAvailability.InsufficientData => "Not enough recent matches yet.",
        DnaAvailability.RequiresParsedMatches => "Needs parsed match data.",
        DnaAvailability.RequiresReplay => "Needs replay analysis.",
        DnaAvailability.NotYetImplemented => "Not yet implemented.",
        _ => "",
    };

    private void RaiseAllPropertiesChanged([CallerMemberName] string? _ = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

public sealed record HeroComfortScoreDisplay(int HeroId, string ScoreText, string GamesText);

public sealed record DnaDimensionDisplay(string DimensionName, string ScoreText, string AvailabilityNote);
