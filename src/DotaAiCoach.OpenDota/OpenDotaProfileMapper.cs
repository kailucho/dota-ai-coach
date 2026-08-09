using System.Text.Json;
using DotaAiCoach.OpenDota.Wire;
using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Identity;
using DotaAiCoach.PlayerProfile.Model;
using DotaAiCoach.PlayerProfile.Scoring;

namespace DotaAiCoach.OpenDota;

/// <summary>
/// Pure mapping from raw OpenDota JSON responses to PlayerProfileSnapshot.
/// Takes strings, not a live client, so it is fully testable from captured
/// fixtures with no network dependency.
/// </summary>
public static class OpenDotaProfileMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PlayerProfileSnapshot Map(
        SteamId id,
        RawFetchResult playerResult,
        RawFetchResult? matchesResult,
        RawFetchResult? recentMatchesResult,
        RawFetchResult? winLossResult,
        RawFetchResult? heroesResult,
        HeroComfortScorer comfortScorer,
        DotaDnaBuilder dnaBuilder,
        DateTimeOffset fetchedAtUtc)
    {
        if (playerResult.StatusCode == 404)
        {
            return new PlayerProfileSnapshot
            {
                Id = id,
                Availability = ProfileAvailability.PlayerNotFound,
                Provenance = new ProfileProvenance { FetchedAtUtc = fetchedAtUtc, SourceName = "opendota" },
            };
        }

        var player = Deserialize<OpenDotaPlayerDto>(playerResult.RawJson);
        if (player is null)
        {
            return new PlayerProfileSnapshot
            {
                Id = id,
                Availability = ProfileAvailability.FetchFailed,
                Provenance = new ProfileProvenance { FetchedAtUtc = fetchedAtUtc, SourceName = "opendota" },
            };
        }

        var matches = Deserialize<List<OpenDotaMatchDto>>(matchesResult?.RawJson) ?? new List<OpenDotaMatchDto>();
        var recentMatches = Deserialize<List<OpenDotaMatchDto>>(recentMatchesResult?.RawJson) ?? new List<OpenDotaMatchDto>();
        var winLoss = Deserialize<OpenDotaWinLossDto>(winLossResult?.RawJson);
        var heroPerformance = Deserialize<List<OpenDotaHeroPerformanceDto>>(heroesResult?.RawJson) ?? new List<OpenDotaHeroPerformanceDto>();

        var wins = winLoss?.Win ?? 0;
        var losses = winLoss?.Lose ?? 0;

        var isPrivate = player.Profile?.FhUnavailable == true && matches.Count == 0 && wins == 0 && losses == 0;

        if (isPrivate)
        {
            return new PlayerProfileSnapshot
            {
                Id = id,
                PersonaName = player.Profile?.PersonaName,
                AvatarUrl = player.Profile?.AvatarFull,
                RankTier = player.RankTier,
                LeaderboardRank = player.LeaderboardRank,
                Availability = ProfileAvailability.MatchHistoryPrivate,
                Provenance = new ProfileProvenance { FetchedAtUtc = fetchedAtUtc, SourceName = "opendota" },
            };
        }

        var dedupedMatches = DedupeMatches(matches, recentMatches);
        var overallGames = wins + losses;

        var heroStats = heroPerformance
            .Where(h => h.HeroId is not null)
            .Select(h => new HeroProfileStats
            {
                HeroId = h.HeroId!.Value,
                Games = h.Games,
                Wins = h.Win,
                LastPlayed = h.LastPlayed is long unixSeconds ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds) : null,
            })
            .ToArray();

        var comfortScores = heroStats
            .Select(stats => comfortScorer.Score(
                ProfileAvailability.Available, stats.HeroId, stats, overallGames, wins,
                dedupedMatches.Where(m => m.HeroId == stats.HeroId).ToArray()))
            .ToArray();

        var dna = dnaBuilder.Build(ProfileAvailability.Available, dedupedMatches, overallGames, wins);

        return new PlayerProfileSnapshot
        {
            Id = id,
            PersonaName = player.Profile?.PersonaName,
            AvatarUrl = player.Profile?.AvatarFull,
            RankTier = player.RankTier,
            LeaderboardRank = player.LeaderboardRank,
            Availability = ProfileAvailability.Available,
            RankedWins = wins,
            RankedLosses = losses,
            Heroes = heroStats,
            RecentMatches = dedupedMatches,
            Dna = dna,
            Provenance = new ProfileProvenance { FetchedAtUtc = fetchedAtUtc, SourceName = "opendota" },
        };
    }

    private static IReadOnlyList<PlayerMatchSummary> DedupeMatches(List<OpenDotaMatchDto> matches, List<OpenDotaMatchDto> recentMatches)
    {
        var byId = new Dictionary<long, OpenDotaMatchDto>();

        // recentMatches carries richer fields (gpm/xpm/damage/lane); add it
        // first so a duplicate from `matches` never overwrites the richer record.
        foreach (var m in recentMatches.Where(m => m.MatchId is not null))
        {
            byId[m.MatchId!.Value] = m;
        }
        foreach (var m in matches.Where(m => m.MatchId is not null))
        {
            byId.TryAdd(m.MatchId!.Value, m);
        }

        return byId.Values.Select(ToSummary).ToArray();
    }

    private static PlayerMatchSummary ToSummary(OpenDotaMatchDto dto)
    {
        bool? playerWon = dto.PlayerSlot is not null && dto.RadiantWin is not null
            ? (dto.PlayerSlot.Value < 128) == dto.RadiantWin.Value
            : null;

        return new PlayerMatchSummary
        {
            MatchId = dto.MatchId,
            HeroId = dto.HeroId,
            PlayerSlot = dto.PlayerSlot,
            RadiantWin = dto.RadiantWin,
            PlayerWon = playerWon,
            GameMode = dto.GameMode,
            LobbyType = dto.LobbyType,
            IsTurbo = dto.GameMode == 22,
            StartTime = dto.StartTime is long unixSeconds ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds) : null,
            DurationSeconds = dto.Duration,
            Kills = dto.Kills,
            Deaths = dto.Deaths,
            Assists = dto.Assists,
            GoldPerMin = dto.GoldPerMin,
            XpPerMin = dto.XpPerMin,
            LastHits = dto.LastHits,
            HeroDamage = dto.HeroDamage,
            TowerDamage = dto.TowerDamage,
            HeroHealing = dto.HeroHealing,
            Lane = dto.Lane,
            LaneRole = dto.LaneRole,
            IsRoaming = dto.IsRoaming,
            AverageRank = dto.AverageRank,
            PartySize = dto.PartySize,
            LeaverStatus = dto.LeaverStatus,
        };
    }

    private static T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);
}

public sealed record RawFetchResult(string? RawJson, int? StatusCode);
