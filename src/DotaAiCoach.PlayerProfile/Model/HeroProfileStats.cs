namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>Aggregated per-hero stats from OpenDota's /players/{id}/heroes, joined with recent-match detail.</summary>
public sealed record HeroProfileStats
{
    public required int HeroId { get; init; }
    public int? Games { get; init; }
    public int? Wins { get; init; }
    public DateTimeOffset? LastPlayed { get; init; }

    /// <summary>Games/wins within the recency window used by scoring, not the lifetime total above.</summary>
    public int? RecentGames { get; init; }
    public int? RecentWins { get; init; }

    public IReadOnlyList<int> ObservedLaneRoles { get; init; } = Array.Empty<int>();
}
