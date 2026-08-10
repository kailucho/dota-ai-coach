using System.Collections.Immutable;
using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;

namespace DotaAiCoach.FeatureEngine;

public interface IFeatureEngine
{
    LiveFeatureSet Build(MatchState state, FeedHealth health, IReadOnlyList<DomainEvent> recentEvents);
}

/// <summary>
/// Transforms MatchState (plus feed health and recent events) into
/// LiveFeatureSet — the recommendation layer's only input. Pure, no I/O.
/// Centralizes the stale/game-state usability check here (IsUsable) so
/// every downstream rule doesn't have to re-derive it independently.
/// </summary>
public sealed class FeatureEngine : IFeatureEngine
{
    public LiveFeatureSet Build(MatchState state, FeedHealth health, IReadOnlyList<DomainEvent> recentEvents)
    {
        var isUsable = health.Status == FeedStatus.Live && state.Match.GameState == GameState.GameInProgress;

        if (!isUsable)
        {
            return LiveFeatureSet.Unusable(state.Match.GameState, health.IsStale);
        }

        var clock = state.Match.ClockTime;
        double? gameMinutes = clock is { } c && c >= 0 ? c / 60.0 : null;

        double? lastHitsPerMinute = state.Player.LastHits is { } lh && gameMinutes is { } gm && gm > 0
            ? lh / gm
            : null;

        var ownedItems = state.Items.OwnedItemNames().ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

        var (hasBuyback, goldShort) = ComputeBuybackAvailability(state);

        return new LiveFeatureSet
        {
            IsUsable = true,
            FeedIsStale = false,
            GameState = state.Match.GameState,
            ClockSeconds = clock,
            GameMinutes = gameMinutes,

            HeroName = state.Hero.Name,
            HeroId = state.Hero.Id,
            HeroLevel = state.Hero.Level,
            IsAlive = state.Hero.Alive,
            RespawnSeconds = state.Hero.RespawnSeconds,
            HealthFraction = state.Hero.HealthFraction,
            ManaFraction = state.Hero.ManaFraction,

            TotalGold = state.Player.Gold,
            Gpm = state.Player.Gpm,
            Xpm = state.Player.Xpm,
            LastHits = state.Player.LastHits,
            Denies = state.Player.Denies,
            LastHitsPerMinute = lastHitsPerMinute,
            Kills = state.Player.Kills,
            Deaths = state.Player.Deaths,
            Assists = state.Player.Assists,

            OwnedItems = ownedItems,
            OwnedItemCount = ownedItems.Count,

            HasBuybackAvailable = hasBuyback,
            BuybackCost = state.Hero.BuybackCost,
            BuybackCooldown = state.Hero.BuybackCooldown,
            GoldShortOfBuyback = goldShort,

            Team = state.Player.Team,
            RadiantScore = state.Match.RadiantScore,
            DireScore = state.Match.DireScore,

            RecentEvents = recentEvents
        };
    }

    /// <summary>
    /// Null-propagating: buyback availability requires both a known gold
    /// total and a known buyback cost. Given buyback_cost's unreliable GSI
    /// population (see HeroSnapshot), an undecidable state must never
    /// collapse to "unavailable."
    /// </summary>
    private static (bool? HasBuyback, int? GoldShort) ComputeBuybackAvailability(MatchState state)
    {
        if (state.Player.Gold is not { } gold || state.Hero.BuybackCost is not { } cost)
        {
            return (null, null);
        }

        var offCooldown = (state.Hero.BuybackCooldown ?? 0) <= 0;
        var canAfford = gold >= cost;
        var goldShort = canAfford ? (int?)null : cost - gold;

        return (offCooldown && canAfford, goldShort);
    }
}
