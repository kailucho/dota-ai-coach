using DotaAiCoach.FairPlay;

namespace DotaAiCoach.Domain.Match;

/// <summary>
/// The local player's hero state, normalized from GSI's <c>hero</c>
/// section.
///
/// <para>
/// <see cref="BuybackCost"/> and <see cref="BuybackCooldown"/> are flagged
/// "verify via fixture" per docs/RESEARCH.md — community GSI libraries
/// confirm the field names exist, but their population is inconsistent in
/// practice (often absent while the hero is alive). Treat null here as
/// "undecidable," never as "no buyback cost"/zero.
/// </para>
/// <para>
/// <see cref="Xp"/> is also flagged "verify via fixture": whether it is
/// cumulative total XP or XP-into-current-level has not been confirmed
/// against a live capture.
/// </para>
/// </summary>
[FairPlaySource(DataSourceKind.Gsi, FairPlayClassification.LiveAllowed,
    Visibility = "GSI hero section — own hero level/HP/mana/buyback, shown in the player's own HUD.",
    Availability = "Whenever a GSI feed is connected and the player's hero is loaded.",
    Justification = "GSI in playing mode only exposes the local player's own hero state.")]
public sealed record HeroSnapshot
{
    /// <summary>Internal hero key, e.g. "npc_dota_hero_antimage" — the correct join key against static hero reference data, not a display name.</summary>
    public string? Name { get; init; }

    public int? Id { get; init; }
    public int? Level { get; init; }

    /// <summary>VERIFY-VIA-FIXTURE: cumulative-vs-into-level semantics unconfirmed.</summary>
    public int? Xp { get; init; }

    public bool? Alive { get; init; }
    public int? RespawnSeconds { get; init; }

    /// <summary>VERIFY-VIA-FIXTURE: population reliability observed as inconsistent.</summary>
    public int? BuybackCost { get; init; }

    /// <summary>VERIFY-VIA-FIXTURE: population reliability observed as inconsistent.</summary>
    public int? BuybackCooldown { get; init; }

    public int? Health { get; init; }
    public int? MaxHealth { get; init; }
    public int? Mana { get; init; }
    public int? MaxMana { get; init; }

    /// <summary>Null unless both health values are known and MaxHealth is positive — never divides by an unchecked value.</summary>
    public double? HealthFraction =>
        Health is { } h && MaxHealth is { } mh && mh > 0 ? (double)h / mh : null;

    /// <summary>Null unless both mana values are known and MaxMana is positive.</summary>
    public double? ManaFraction =>
        Mana is { } m && MaxMana is { } mm && mm > 0 ? (double)m / mm : null;

    public static readonly HeroSnapshot Empty = new();
}
