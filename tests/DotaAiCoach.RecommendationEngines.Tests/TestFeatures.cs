using System.Collections.Immutable;
using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Tests;

/// <summary>Small helper for building a usable LiveFeatureSet with sensible defaults, overridable per test.</summary>
internal static class TestFeatures
{
    public static LiveFeatureSet Usable(
        int? gold = null,
        int? gpm = null,
        IReadOnlyList<string>? ownedItems = null,
        bool? hasBuyback = null,
        int? buybackCooldown = null,
        bool? isAlive = null,
        double? healthFraction = null,
        double? gameMinutes = null,
        IReadOnlyList<DomainEvent>? recentEvents = null,
        int? heroLevel = null,
        int? heroId = null,
        string? team = null,
        int? radiantScore = null,
        int? direScore = null) => new()
    {
        IsUsable = true,
        FeedIsStale = false,
        GameState = GameState.GameInProgress,
        ClockSeconds = gameMinutes is { } m ? (int)(m * 60) : null,
        GameMinutes = gameMinutes,
        TotalGold = gold,
        Gpm = gpm,
        OwnedItems = (ownedItems ?? Array.Empty<string>()).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
        OwnedItemCount = ownedItems?.Count ?? 0,
        HasBuybackAvailable = hasBuyback,
        BuybackCooldown = buybackCooldown,
        IsAlive = isAlive,
        HealthFraction = healthFraction,
        RecentEvents = recentEvents ?? Array.Empty<DomainEvent>(),
        HeroLevel = heroLevel,
        HeroId = heroId,
        Team = team,
        RadiantScore = radiantScore,
        DireScore = direScore
    };
}
