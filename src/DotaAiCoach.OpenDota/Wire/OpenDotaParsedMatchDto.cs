using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota.Wire;

/// <summary>
/// Raw shape of GET /matches/{match_id} AFTER a parse job has completed
/// (POST /request/{match_id}). Verified against a real captured fixture
/// (tests/DotaAiCoach.OpenDota.Tests/Fixtures/matches_8936354085_parsed.json)
/// — only the fields the six newly-computed DNA dimensions actually need
/// are modeled here, not the full captured shape (objectives/teamfights/
/// chat/etc. are out of scope for v1), per this project's narrow-modeling
/// convention.
/// </summary>
internal sealed class OpenDotaParsedMatchDto
{
    [JsonPropertyName("match_id")] public long? MatchId { get; init; }
    [JsonPropertyName("players")] public List<OpenDotaParsedPlayerDto>? Players { get; init; }
}

internal sealed class OpenDotaParsedPlayerDto
{
    [JsonPropertyName("account_id")] public long? AccountId { get; init; }
    [JsonPropertyName("player_slot")] public int? PlayerSlot { get; init; }
    [JsonPropertyName("hero_id")] public int? HeroId { get; init; }

    [JsonPropertyName("purchase_log")] public List<PurchaseLogEntryDto>? PurchaseLog { get; init; }
    [JsonPropertyName("buyback_log")] public List<BuybackLogEntryDto>? BuybackLog { get; init; }
    [JsonPropertyName("buyback_count")] public int? BuybackCount { get; init; }

    [JsonPropertyName("obs_placed")] public int? ObsPlaced { get; init; }
    [JsonPropertyName("sen_placed")] public int? SenPlaced { get; init; }

    [JsonPropertyName("lane_efficiency_pct")] public double? LaneEfficiencyPct { get; init; }
    [JsonPropertyName("lane_kills")] public int? LaneKills { get; init; }

    [JsonPropertyName("tower_kills")] public int? TowerKills { get; init; }
    [JsonPropertyName("roshan_kills")] public int? RoshanKills { get; init; }
    [JsonPropertyName("roshans_killed")] public int? RoshansKilled { get; init; }

    [JsonPropertyName("gold_t")] public List<int>? GoldTimeSeries { get; init; }
    [JsonPropertyName("xp_t")] public List<int>? XpTimeSeries { get; init; }
}

internal sealed class PurchaseLogEntryDto
{
    [JsonPropertyName("time")] public int? Time { get; init; }
    [JsonPropertyName("key")] public string? Key { get; init; }
}

internal sealed class BuybackLogEntryDto
{
    [JsonPropertyName("time")] public int? Time { get; init; }
}
