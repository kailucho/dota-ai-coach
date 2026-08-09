namespace DotaAiCoach.Domain.Events;

/// <summary>
/// A discrete state transition derived by the GSI normalizer by comparing
/// consecutive MatchState snapshots. Kept deliberately small — only events
/// this vertical slice's rule engine actually consumes — per dota-gsi's
/// guidance to grow the event vocabulary only as real consumers need it.
/// </summary>
public abstract record DomainEvent(int? ClockTime, DateTimeOffset OccurredAt);

public sealed record HeroLeveledUp(int FromLevel, int ToLevel, int? ClockTime, DateTimeOffset OccurredAt)
    : DomainEvent(ClockTime, OccurredAt);

public enum ItemLocation
{
    Inventory,
    Stash,
    Teleport,
    Neutral
}

public sealed record ItemPurchased(string ItemName, ItemLocation Location, int? ClockTime, DateTimeOffset OccurredAt)
    : DomainEvent(ClockTime, OccurredAt);

/// <summary>
/// Fires only when CanBuybackNow can actually be computed (both gold and
/// buyback_cost are known) and its value flips. Given buyback_cost's
/// unreliable population (see HeroSnapshot), an undecidable state must
/// never be treated as "false" here.
/// </summary>
public sealed record BuybackStatusChanged(
    bool CanBuybackNow,
    int? BuybackCost,
    int? BuybackCooldown,
    int? ClockTime,
    DateTimeOffset OccurredAt) : DomainEvent(ClockTime, OccurredAt);

public sealed record HeroDiedOrRespawned(bool IsAlive, int? RespawnSeconds, int? ClockTime, DateTimeOffset OccurredAt)
    : DomainEvent(ClockTime, OccurredAt);

public sealed record GameStateChanged(
    DotaAiCoach.Domain.Match.GameState From,
    DotaAiCoach.Domain.Match.GameState To,
    string? RawTo,
    int? ClockTime,
    DateTimeOffset OccurredAt) : DomainEvent(ClockTime, OccurredAt);

public sealed record FeedDiscontinuityDetected(double GapSeconds, int? ClockTime, DateTimeOffset OccurredAt)
    : DomainEvent(ClockTime, OccurredAt);
