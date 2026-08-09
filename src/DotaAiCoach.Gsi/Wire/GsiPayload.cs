using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotaAiCoach.Gsi.Wire;

/// <summary>
/// The raw GSI HTTP POST body, deserialized as-is. Every field is
/// nullable: GSI payloads are partial by design (a POST may contain only
/// some top-level sections), and Valve publishes no official schema —
/// field names here are sourced from community GSI libraries
/// (antonpup/Dota2GSI, xzion/dota2-gsi) per docs/RESEARCH.md, never
/// invented. This is playing-mode shape only (flat sections); spectating
/// mode's nested per-player shape is explicitly out of scope for this
/// slice.
/// </summary>
public sealed record GsiPayload
{
    [JsonPropertyName("provider")] public GsiProvider? Provider { get; init; }
    [JsonPropertyName("map")] public GsiMap? Map { get; init; }
    [JsonPropertyName("player")] public GsiPlayer? Player { get; init; }
    [JsonPropertyName("hero")] public GsiHero? Hero { get; init; }
    [JsonPropertyName("items")] public GsiItems? Items { get; init; }
    [JsonPropertyName("abilities")] public GsiAbilities? Abilities { get; init; }
    [JsonPropertyName("auth")] public GsiAuth? Auth { get; init; }

    // "previously"/"added" are the client's own diff hints. Per dota-gsi's
    // state-diffing guidance, our normalizer derives events by comparing
    // against our own accumulated MatchState, not against these — they are
    // preserved as raw elements only in case a future feature needs them,
    // never parsed into typed models in this slice.
    [JsonPropertyName("previously")] public JsonElement? Previously { get; init; }
    [JsonPropertyName("added")] public JsonElement? Added { get; init; }
}

public sealed record GsiProvider
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("appid")] public int? AppId { get; init; }
    [JsonPropertyName("version")] public int? Version { get; init; }
    [JsonPropertyName("timestamp")] public long? Timestamp { get; init; }
}

public sealed record GsiAuth
{
    [JsonPropertyName("token")] public string? Token { get; init; }
}

public sealed record GsiMap
{
    [JsonPropertyName("matchid")] public string? MatchId { get; init; }
    [JsonPropertyName("game_time")] public int? GameTime { get; init; }
    [JsonPropertyName("clock_time")] public int? ClockTime { get; init; }
    [JsonPropertyName("daytime")] public bool? Daytime { get; init; }
    [JsonPropertyName("game_state")] public string? GameState { get; init; }
    [JsonPropertyName("paused")] public bool? Paused { get; init; }
    [JsonPropertyName("radiant_score")] public int? RadiantScore { get; init; }
    [JsonPropertyName("dire_score")] public int? DireScore { get; init; }
    [JsonPropertyName("win_team")] public string? WinTeam { get; init; }
}

public sealed record GsiPlayer
{
    [JsonPropertyName("steamid")] public string? SteamId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("team_name")] public string? TeamName { get; init; }
    [JsonPropertyName("kills")] public int? Kills { get; init; }
    [JsonPropertyName("deaths")] public int? Deaths { get; init; }
    [JsonPropertyName("assists")] public int? Assists { get; init; }
    [JsonPropertyName("last_hits")] public int? LastHits { get; init; }
    [JsonPropertyName("denies")] public int? Denies { get; init; }
    [JsonPropertyName("gold")] public int? Gold { get; init; }
    [JsonPropertyName("gold_reliable")] public int? GoldReliable { get; init; }
    [JsonPropertyName("gold_unreliable")] public int? GoldUnreliable { get; init; }
    [JsonPropertyName("gpm")] public int? Gpm { get; init; }
    [JsonPropertyName("xpm")] public int? Xpm { get; init; }
    [JsonPropertyName("net_worth")] public int? NetWorth { get; init; }
}

public sealed record GsiHero
{
    [JsonPropertyName("id")] public int? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("level")] public int? Level { get; init; }
    [JsonPropertyName("xp")] public int? Xp { get; init; }
    [JsonPropertyName("alive")] public bool? Alive { get; init; }
    [JsonPropertyName("respawn_seconds")] public int? RespawnSeconds { get; init; }
    [JsonPropertyName("buyback_cost")] public int? BuybackCost { get; init; }
    [JsonPropertyName("buyback_cooldown")] public int? BuybackCooldown { get; init; }
    [JsonPropertyName("health")] public int? Health { get; init; }
    [JsonPropertyName("max_health")] public int? MaxHealth { get; init; }
    [JsonPropertyName("mana")] public int? Mana { get; init; }
    [JsonPropertyName("max_mana")] public int? MaxMana { get; init; }
}
