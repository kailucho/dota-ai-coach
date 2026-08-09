using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota.Wire;

/// <summary>Raw shape of one entry from GET /heroes (static reference data).</summary>
internal sealed class OpenDotaHeroDto
{
    [JsonPropertyName("id")] public int? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("localized_name")] public string? LocalizedName { get; init; }
    [JsonPropertyName("primary_attr")] public string? PrimaryAttr { get; init; }
    [JsonPropertyName("attack_type")] public string? AttackType { get; init; }
    [JsonPropertyName("roles")] public List<string>? Roles { get; init; }
}
