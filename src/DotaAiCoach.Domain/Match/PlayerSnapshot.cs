using DotaAiCoach.FairPlay;

namespace DotaAiCoach.Domain.Match;

/// <summary>
/// The local player's own scoreboard/economy state, normalized from GSI's
/// <c>player</c> section. This is the player's own data only (playing
/// mode, not spectating) — see docs/FAIR_PLAY.md.
/// </summary>
[FairPlaySource(DataSourceKind.Gsi, FairPlayClassification.LiveAllowed,
    Visibility = "GSI player section — own KDA/gold/GPM/XPM, shown in the player's own scoreboard/HUD.",
    Availability = "Whenever a GSI feed is connected and the player is in an active match.",
    Justification = "GSI in playing mode only exposes the local player's own state, mirroring what that player's own scoreboard already shows them.")]
public sealed record PlayerSnapshot
{
    public string? SteamId { get; init; }
    public string? Name { get; init; }

    /// <summary>Raw team string as received ("radiant"/"dire").</summary>
    public string? Team { get; init; }

    public int? Kills { get; init; }
    public int? Deaths { get; init; }
    public int? Assists { get; init; }
    public int? LastHits { get; init; }
    public int? Denies { get; init; }
    public int? Gold { get; init; }
    public int? GoldReliable { get; init; }
    public int? GoldUnreliable { get; init; }
    public int? Gpm { get; init; }
    public int? Xpm { get; init; }
    public int? NetWorth { get; init; }

    public static readonly PlayerSnapshot Empty = new();
}
