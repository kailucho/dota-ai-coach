using DotaAiCoach.FairPlay;
using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Identity;

namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>
/// A player's public OpenDota profile plus derived scoring. Sourced
/// entirely from OpenDota's public API — information any Dota player
/// could look up themselves outside the game client.
/// </summary>
[FairPlaySource(DataSourceKind.ExternalApi, FairPlayClassification.PublicPreflight,
    Visibility = "OpenDota's public player/match-history endpoints — the same data any player could look up themselves via opendota.com.",
    Availability = "Whenever the player's Steam profile has 'Expose Public Match Data' enabled and OpenDota has indexed their matches.",
    Justification = "OpenDota only republishes publicly-exposed Dota match data; nothing here is hidden game state or another player's private information. Not currently consumed by any live recommendation (see docs/adr/0012).")]
public sealed record PlayerProfileSnapshot
{
    public SteamId? Id { get; init; }
    public string? PersonaName { get; init; }
    public string? AvatarUrl { get; init; }
    public int? RankTier { get; init; }
    public int? LeaderboardRank { get; init; }

    public ProfileAvailability Availability { get; init; } = ProfileAvailability.Unknown;

    public int? RankedWins { get; init; }
    public int? RankedLosses { get; init; }

    public IReadOnlyList<HeroProfileStats> Heroes { get; init; } = Array.Empty<HeroProfileStats>();
    public IReadOnlyList<PlayerMatchSummary> RecentMatches { get; init; } = Array.Empty<PlayerMatchSummary>();

    public DotaDnaProfile? Dna { get; init; }
    public ProfileProvenance Provenance { get; init; } = ProfileProvenance.Empty;

    public static readonly PlayerProfileSnapshot Empty = new();
}
