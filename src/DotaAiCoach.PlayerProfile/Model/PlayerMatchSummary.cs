namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>
/// One deduplicated match from the player's public history. Fields only
/// present on OpenDota's recentMatches variant (GPM/XPM/damage/lane) are
/// nullable and absent for matches sourced from the plain matches list.
/// </summary>
public sealed record PlayerMatchSummary
{
    public long? MatchId { get; init; }
    public int? HeroId { get; init; }
    public int? PlayerSlot { get; init; }
    public bool? RadiantWin { get; init; }

    /// <summary>Derived: (PlayerSlot &lt; 128) == RadiantWin. Null if either input is null.</summary>
    public bool? PlayerWon { get; init; }

    public int? GameMode { get; init; }
    public int? LobbyType { get; init; }

    /// <summary>Derived from GameMode == 22. Turbo is identified by game_mode, not lobby_type.</summary>
    public bool IsTurbo { get; init; }

    public DateTimeOffset? StartTime { get; init; }
    public int? DurationSeconds { get; init; }
    public int? Kills { get; init; }
    public int? Deaths { get; init; }
    public int? Assists { get; init; }

    public int? GoldPerMin { get; init; }
    public int? XpPerMin { get; init; }
    public int? LastHits { get; init; }
    public int? HeroDamage { get; init; }
    public int? TowerDamage { get; init; }
    public int? HeroHealing { get; init; }
    public int? Lane { get; init; }
    public int? LaneRole { get; init; }
    public bool? IsRoaming { get; init; }

    public int? AverageRank { get; init; }
    public int? PartySize { get; init; }
    public int? LeaverStatus { get; init; }

    public static readonly PlayerMatchSummary Empty = new();
}
