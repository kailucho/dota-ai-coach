using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota.Wire;

/// <summary>
/// Raw shape of GET /players/{account_id}, verified against a live
/// response (see docs/RESEARCH.md-equivalent verification for OpenDota:
/// this is not from memory, it was checked against the real API before
/// being written). Every field nullable — OpenDota's contract is not
/// formally versioned.
/// </summary>
internal sealed class OpenDotaPlayerDto
{
    [JsonPropertyName("profile")] public OpenDotaProfileDto? Profile { get; init; }
    [JsonPropertyName("rank_tier")] public int? RankTier { get; init; }
    [JsonPropertyName("leaderboard_rank")] public int? LeaderboardRank { get; init; }
    [JsonPropertyName("computed_mmr")] public double? ComputedMmr { get; init; }
}

internal sealed class OpenDotaProfileDto
{
    [JsonPropertyName("account_id")] public long? AccountId { get; init; }
    [JsonPropertyName("personaname")] public string? PersonaName { get; init; }
    [JsonPropertyName("steamid")] public string? SteamId { get; init; }
    [JsonPropertyName("avatarfull")] public string? AvatarFull { get; init; }
    [JsonPropertyName("profileurl")] public string? ProfileUrl { get; init; }

    /// <summary>
    /// Community-documented as "full history unavailable", but observed in
    /// practice to be true even for accounts with a complete, public match
    /// history (see the Dendi fixture) — it is NOT a reliable private-
    /// profile signal on its own. See OpenDotaProfileMapper for the actual
    /// (compound) private-profile detection rule.
    /// </summary>
    [JsonPropertyName("fh_unavailable")] public bool? FhUnavailable { get; init; }
}
