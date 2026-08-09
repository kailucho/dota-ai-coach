using System.Collections.Immutable;
using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.FairPlay;

namespace DotaAiCoach.FeatureEngine;

/// <summary>
/// Normalized, model/rule-agnostic input to the recommendation layer. This
/// is the mandatory seam between MatchState and RecommendationEngines per
/// dota-architecture's rule 6 — recommendation logic never reads
/// MatchState directly, only this. Every field here is derivable solely
/// from the local player's own GSI feed, so the whole type is LiveAllowed.
/// </summary>
[FairPlaySource(DataSourceKind.Gsi, FairPlayClassification.LiveAllowed,
    Visibility = "Derived solely from the player's own GSI feed (own hero/player/items), all shown on the player's own HUD.",
    Availability = "Whenever a GSI feed is live and a match is in progress.",
    Justification = "Every input is the player's own state; no field here is reachable from any other data source.")]
public sealed record LiveFeatureSet : ILiveRecommendationInput
{
    /// <summary>False whenever the feed is stale or the match isn't actively in progress — per dota-gsi's stale-suppression requirement, rules must not fire when this is false.</summary>
    public required bool IsUsable { get; init; }

    public required bool FeedIsStale { get; init; }
    public required GameState GameState { get; init; }

    public int? ClockSeconds { get; init; }
    public double? GameMinutes { get; init; }

    public string? HeroName { get; init; }
    public int? HeroLevel { get; init; }
    public bool? IsAlive { get; init; }
    public int? RespawnSeconds { get; init; }
    public double? HealthFraction { get; init; }
    public double? ManaFraction { get; init; }

    public int? TotalGold { get; init; }
    public int? Gpm { get; init; }
    public int? Xpm { get; init; }
    public int? LastHits { get; init; }
    public int? Denies { get; init; }
    public double? LastHitsPerMinute { get; init; }
    public int? Kills { get; init; }
    public int? Deaths { get; init; }
    public int? Assists { get; init; }

    public IReadOnlySet<string> OwnedItems { get; init; } = ImmutableHashSet<string>.Empty;
    public int OwnedItemCount { get; init; }

    /// <summary>Null when undecidable (buyback cost unknown, or gold unknown) — never defaulted to false.</summary>
    public bool? HasBuybackAvailable { get; init; }

    public int? BuybackCost { get; init; }
    public int? BuybackCooldown { get; init; }
    public int? GoldShortOfBuyback { get; init; }

    public IReadOnlyList<DomainEvent> RecentEvents { get; init; } = Array.Empty<DomainEvent>();

    public static LiveFeatureSet Unusable(GameState state, bool stale) => new()
    {
        IsUsable = false,
        FeedIsStale = stale,
        GameState = state
    };
}
