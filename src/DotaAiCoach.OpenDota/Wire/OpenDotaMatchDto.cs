using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota.Wire;

/// <summary>
/// One entry from GET /players/{id}/matches or /players/{id}/recentMatches.
/// recentMatches carries additional performance fields (gpm/xpm/damage/
/// lane) that plain matches does not — all such fields nullable so one DTO
/// covers both endpoints.
/// </summary>
internal sealed class OpenDotaMatchDto
{
    [JsonPropertyName("match_id")] public long? MatchId { get; init; }
    [JsonPropertyName("player_slot")] public int? PlayerSlot { get; init; }
    [JsonPropertyName("radiant_win")] public bool? RadiantWin { get; init; }
    [JsonPropertyName("duration")] public int? Duration { get; init; }
    [JsonPropertyName("game_mode")] public int? GameMode { get; init; }
    [JsonPropertyName("lobby_type")] public int? LobbyType { get; init; }
    [JsonPropertyName("hero_id")] public int? HeroId { get; init; }
    [JsonPropertyName("start_time")] public long? StartTime { get; init; }
    [JsonPropertyName("kills")] public int? Kills { get; init; }
    [JsonPropertyName("deaths")] public int? Deaths { get; init; }
    [JsonPropertyName("assists")] public int? Assists { get; init; }
    [JsonPropertyName("average_rank")] public int? AverageRank { get; init; }
    [JsonPropertyName("leaver_status")] public int? LeaverStatus { get; init; }
    [JsonPropertyName("party_size")] public int? PartySize { get; init; }

    // recentMatches-only fields.
    [JsonPropertyName("xp_per_min")] public int? XpPerMin { get; init; }
    [JsonPropertyName("gold_per_min")] public int? GoldPerMin { get; init; }
    [JsonPropertyName("hero_damage")] public int? HeroDamage { get; init; }
    [JsonPropertyName("tower_damage")] public int? TowerDamage { get; init; }
    [JsonPropertyName("hero_healing")] public int? HeroHealing { get; init; }
    [JsonPropertyName("last_hits")] public int? LastHits { get; init; }
    [JsonPropertyName("lane")] public int? Lane { get; init; }
    [JsonPropertyName("lane_role")] public int? LaneRole { get; init; }
    [JsonPropertyName("is_roaming")] public bool? IsRoaming { get; init; }
}
