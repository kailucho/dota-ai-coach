using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota.Wire;

/// <summary>Raw shape of one entry from GET /players/{id}/heroes.</summary>
internal sealed class OpenDotaHeroPerformanceDto
{
    [JsonPropertyName("hero_id")] public int? HeroId { get; init; }
    [JsonPropertyName("last_played")] public long? LastPlayed { get; init; }
    [JsonPropertyName("games")] public int? Games { get; init; }
    [JsonPropertyName("win")] public int? Win { get; init; }
}
