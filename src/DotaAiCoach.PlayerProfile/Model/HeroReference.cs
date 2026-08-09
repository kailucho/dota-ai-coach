using DotaAiCoach.FairPlay;

namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>Static hero reference data from OpenDota's /heroes endpoint (patch-versioned, not per-player).</summary>
[FairPlaySource(DataSourceKind.StaticReference, FairPlayClassification.PublicPreflight,
    Visibility = "Static hero metadata (name, attributes, roles) — public reference data, not tied to any specific player or match.",
    Availability = "Whenever the OpenDota /heroes reference cache is populated.",
    Justification = "Hero reference data is publicly documented game data, independent of any live match.")]
public sealed record HeroReference
{
    public required int Id { get; init; }
    public string? InternalName { get; init; }
    public string? LocalizedName { get; init; }
    public string? PrimaryAttr { get; init; }
    public string? AttackType { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
