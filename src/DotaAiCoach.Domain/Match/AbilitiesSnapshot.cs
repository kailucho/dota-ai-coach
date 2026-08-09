using DotaAiCoach.FairPlay;

namespace DotaAiCoach.Domain.Match;

/// <summary>A single ability slot, normalized from GSI's <c>abilities.abilityN</c>.</summary>
public sealed record AbilitySlot
{
    public string? Name { get; init; }
    public int? Level { get; init; }
    public int? Cooldown { get; init; }
    public bool? CanCast { get; init; }
    public bool? Passive { get; init; }
    public bool? Ultimate { get; init; }
}

/// <summary>The local player's hero abilities, normalized from GSI's <c>abilities</c> section.</summary>
[FairPlaySource(DataSourceKind.Gsi, FairPlayClassification.LiveAllowed,
    Visibility = "GSI abilities section — own ability levels/cooldowns, shown in the player's own HUD.",
    Availability = "Whenever a GSI feed is connected and the player's hero is loaded.",
    Justification = "GSI in playing mode only exposes the local player's own abilities.")]
public sealed record AbilitiesSnapshot
{
    public IReadOnlyList<AbilitySlot> Abilities { get; init; } = Array.Empty<AbilitySlot>();

    public static readonly AbilitiesSnapshot Empty = new();
}
