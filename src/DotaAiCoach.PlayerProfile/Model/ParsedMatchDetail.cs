namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>
/// Fields available only after OpenDota has parsed a match (POST
/// /request/{match_id}), for the requesting player specifically. Decoupled
/// from OpenDota's wire shape (DotaAiCoach.OpenDota.Wire.OpenDotaParsedMatchDto)
/// so DotaDnaBuilder never depends on OpenDota's DTOs — mirrors how
/// PlayerMatchSummary already decouples DNA building from unparsed match
/// summaries. Parsing is expensive and user-triggered (see docs/adr/0016),
/// so most snapshots carry an empty list here.
/// </summary>
public sealed record ParsedMatchDetail
{
    public required long MatchId { get; init; }
    public int? HeroId { get; init; }

    public IReadOnlyList<int> PurchaseTimesSeconds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> BuybackTimesSeconds { get; init; } = Array.Empty<int>();

    public int? ObsPlaced { get; init; }
    public int? SenPlaced { get; init; }

    public double? LaneEfficiencyPct { get; init; }
    public int? LaneKills { get; init; }

    public int? TowerKills { get; init; }
    public int? RoshanKills { get; init; }

    public IReadOnlyList<int> GoldTimeSeries { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> XpTimeSeries { get; init; } = Array.Empty<int>();
}
