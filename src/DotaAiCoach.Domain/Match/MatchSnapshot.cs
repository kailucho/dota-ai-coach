using DotaAiCoach.FairPlay;

namespace DotaAiCoach.Domain.Match;

/// <summary>
/// Match/map-level state, normalized from GSI's <c>map</c> section. Every
/// gameplay field is nullable: absence is a real, distinguishable state
/// (the section wasn't present in the latest partial payload, or the
/// underlying value hasn't been observed yet) — see dota-gsi's
/// state-diffing guidance on never defaulting an absent field to a
/// misleading zero/false.
/// </summary>
[FairPlaySource(DataSourceKind.Gsi, FairPlayClassification.LiveAllowed,
    Visibility = "GSI map section — clock, match id, phase, score; all shown on the player's own HUD.",
    Availability = "Whenever a GSI feed is connected and a match is loaded.",
    Justification = "Directly sourced from the player's own GSI feed, which only exposes match-level state visible to that player.")]
public sealed record MatchSnapshot
{
    public string? MatchId { get; init; }

    /// <summary>Visible HUD clock, in seconds. Negative during pre-horn strategy time.</summary>
    public int? ClockTime { get; init; }

    /// <summary>Time since GSI process start, in seconds. Not the HUD clock — prefer <see cref="ClockTime"/> for player-facing use.</summary>
    public int? GameTime { get; init; }

    public GameState GameState { get; init; } = GameState.Unknown;

    /// <summary>The raw game_state string as received, always preserved even when <see cref="GameState"/> is Unknown.</summary>
    public string? RawGameState { get; init; }

    public bool? DaytimeIsDay { get; init; }
    public bool? Paused { get; init; }
    public int? RadiantScore { get; init; }
    public int? DireScore { get; init; }
    public string? WinTeam { get; init; }

    public static readonly MatchSnapshot Empty = new();
}
