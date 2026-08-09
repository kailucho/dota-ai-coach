namespace DotaAiCoach.Domain.Recommendations;

/// <summary>
/// Presentation priority, per dota-live-coach's P0/P1/P2 tiers. P0 is
/// critical/act-now; P1 actionable; P2 informational.
/// </summary>
public enum MessagePriority
{
    P0 = 0,
    P1 = 1,
    P2 = 2
}

/// <summary>
/// Structured, enumerable reasons a recommendation was produced — what the
/// live-coach message compresses from and what a future explanation layer
/// would narrate, per dota-item-recommender's required output shape. Kept
/// to the small set this slice's rules actually emit.
/// </summary>
public enum ReasonCode
{
    AffordableNow,
    CloseToAffording,
    FarmUntilAffordable,
    CoreProgressionNext,
    CountersEnemyBurst,
    MatchesPowerSpikeWindow,
    NoBuybackAvailable,
    BuybackOnCooldown,
    LowHealthRisk,
    LateGameDeathRisk,
    LevelThresholdReached,
    ItemThresholdReached
}

public sealed record RecommendationAlternative(
    string ItemId,
    double Score,
    IReadOnlyList<ReasonCode> ReasonCodes);

/// <summary>
/// A single rule's output — the structured shape dota-item-recommender
/// requires (itemId/score/confidence/reasonCodes/alternatives), produced
/// before any live-message compression happens. <see cref="DetailText"/>
/// is the expanded-mode explanation; per dota-live-coach it must never
/// enter the live message stream directly.
/// </summary>
public sealed record Recommendation
{
    /// <summary>Which rule produced this — required for traceability/debugging.</summary>
    public required string RuleId { get; init; }

    public required MessagePriority Priority { get; init; }

    /// <summary>The live-stream fragment, per dota-live-coach's message-style rules (short, imperative, no justification clause).</summary>
    public required string Message { get; init; }

    /// <summary>Stable identity used for anti-spam cooldown/dedup — see LiveMessageBudget.</summary>
    public required string MessageKey { get; init; }

    public IReadOnlyList<ReasonCode> ReasonCodes { get; init; } = Array.Empty<ReasonCode>();
    public string? ItemId { get; init; }
    public double Score { get; init; }
    public double Confidence { get; init; } = 0.5;
    public IReadOnlyList<RecommendationAlternative> Alternatives { get; init; } = Array.Empty<RecommendationAlternative>();

    /// <summary>Expanded-mode-only explanation. Never shown in the live message stream.</summary>
    public string? DetailText { get; init; }
}

/// <summary>The filtered, budgeted subset of Recommendations actually surfaced live — see LiveMessageBudget.</summary>
public sealed record LiveMessage(
    string RuleId,
    MessagePriority Priority,
    string Text,
    string MessageKey,
    DateTimeOffset IssuedAt);
