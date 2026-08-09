using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Identity;
using DotaAiCoach.PlayerProfile.Model;
using DotaAiCoach.PlayerProfile.Scoring;

namespace DotaAiCoach.OpenDota;

/// <summary>
/// Orchestrates the full profile fetch: heroes reference (cached long) →
/// player → wl → matches → recentMatches, all through the caching client,
/// then hands the raw bodies to OpenDotaProfileMapper. This is the one
/// entry point that talks to the network stack — everything downstream of
/// it only ever sees a PlayerProfileSnapshot.
/// </summary>
public sealed class PlayerProfileService
{
    private readonly CachingOpenDotaClient _client;
    private readonly OpenDotaOptions _options;
    private readonly HeroComfortScorer _comfortScorer;
    private readonly DotaDnaBuilder _dnaBuilder;
    private readonly TimeProvider _timeProvider;

    public PlayerProfileService(
        CachingOpenDotaClient client,
        OpenDotaOptions options,
        HeroComfortScorer? comfortScorer = null,
        DotaDnaBuilder? dnaBuilder = null,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _options = options;
        _comfortScorer = comfortScorer ?? new HeroComfortScorer();
        _dnaBuilder = dnaBuilder ?? new DotaDnaBuilder();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PlayerProfileSnapshot> FetchAsync(SteamId id, CancellationToken ct)
    {
        var playerPath = $"/players/{id.Steam32}";
        var matchesPath = $"/players/{id.Steam32}/matches?limit={_options.RecentMatchLimit}";
        var recentMatchesPath = $"/players/{id.Steam32}/recentMatches";
        var wlPath = $"/players/{id.Steam32}/wl";
        var heroesPath = $"/players/{id.Steam32}/heroes";

        var (playerResponse, staleAvailability) = await _client.GetWithAvailabilityAsync(playerPath, _options.ProfileTtl, ct).ConfigureAwait(false);

        if (staleAvailability is ProfileAvailability.Unknown or ProfileAvailability.FetchFailed && playerResponse.RawJson is null)
        {
            return new PlayerProfileSnapshot
            {
                Id = id,
                Availability = staleAvailability.Value,
                Provenance = new ProfileProvenance { FetchedAtUtc = _timeProvider.GetUtcNow(), SourceName = "opendota" },
            };
        }

        var matchesResponse = await _client.GetAsync(matchesPath, ct).ConfigureAwait(false);
        var recentMatchesResponse = await _client.GetAsync(recentMatchesPath, ct).ConfigureAwait(false);
        var wlResponse = await _client.GetAsync(wlPath, ct).ConfigureAwait(false);
        var heroesResponse = await _client.GetAsync(heroesPath, ct).ConfigureAwait(false);

        var snapshot = OpenDotaProfileMapper.Map(
            id,
            new RawFetchResult(playerResponse.RawJson, playerResponse.StatusCode),
            new RawFetchResult(matchesResponse.RawJson, matchesResponse.StatusCode),
            new RawFetchResult(recentMatchesResponse.RawJson, recentMatchesResponse.StatusCode),
            new RawFetchResult(wlResponse.RawJson, wlResponse.StatusCode),
            new RawFetchResult(heroesResponse.RawJson, heroesResponse.StatusCode),
            _comfortScorer,
            _dnaBuilder,
            _timeProvider.GetUtcNow());

        if (staleAvailability == ProfileAvailability.CacheOnlyStale && snapshot.Availability == ProfileAvailability.Available)
        {
            snapshot = snapshot with { Availability = ProfileAvailability.CacheOnlyStale };
        }

        return snapshot with { Provenance = snapshot.Provenance with { ServedFromCache = playerResponse.ServedFromCache } };
    }

    /// <summary>
    /// Explicit, user-triggered deep refresh: parses up to maxMatches of
    /// the player's most recent ranked matches (POST /request, polled,
    /// then re-fetched), and rebuilds the Dota DNA profile with the
    /// resulting ParsedMatchDetail records. Never called automatically —
    /// see docs/adr/0016. Requires a preceding FetchAsync (or a cached
    /// snapshot) to know which matches exist; pass its result in.
    /// </summary>
    public async Task<PlayerProfileSnapshot> RequestDeepDnaRefreshAsync(
        SteamId id,
        PlayerProfileSnapshot currentSnapshot,
        IOpenDotaClient rawClient,
        int maxMatches,
        IProgress<ParseProgress>? progress,
        CancellationToken ct)
    {
        if (currentSnapshot.Availability != ProfileAvailability.Available)
        {
            return currentSnapshot;
        }

        var matchIds = currentSnapshot.RecentMatches
            .Where(m => !m.IsTurbo && m.MatchId is not null)
            .OrderByDescending(m => m.StartTime)
            .Take(maxMatches)
            .Select(m => m.MatchId!.Value)
            .ToArray();

        var requester = new ParsedMatchRequester(rawClient, _client, _timeProvider);
        await requester.ParseMatchesAsync(matchIds, progress, ct).ConfigureAwait(false);

        var parsedMatches = new List<ParsedMatchDetail>();
        foreach (var matchId in matchIds)
        {
            var rawJson = await _client.GetCachedParsedMatchAsync($"/matches/{matchId}", ct).ConfigureAwait(false);
            var detail = OpenDotaProfileMapper.MapParsedMatch(matchId, rawJson, id.Steam32);
            if (detail is not null)
            {
                parsedMatches.Add(detail);
            }
        }

        var newDna = _dnaBuilder.Build(
            currentSnapshot.Availability,
            currentSnapshot.RecentMatches,
            (currentSnapshot.RankedWins ?? 0) + (currentSnapshot.RankedLosses ?? 0),
            currentSnapshot.RankedWins ?? 0,
            parsedMatches);

        return currentSnapshot with { Dna = newDna };
    }
}
