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
    ItemThresholdReached,
    MlWinProbabilityEstimate,
    EnemyTeamAheadOnKills
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

    /// <summary>
    /// Set only by NextItemRule: the full, display-ready early/mid/late
    /// item plan (see ItemPlan) for the overlay's always-visible panel.
    /// Null for every other rule. Kept on Recommendation rather than a
    /// separate engine output so RuleRecommendationEngine's shape (one
    /// list of Recommendations) doesn't need a second parallel return
    /// value just for this one rule.
    /// </summary>
    public ItemPlan? ItemPlan { get; init; }
}

/// <summary>The filtered, budgeted subset of Recommendations actually surfaced live — see LiveMessageBudget.</summary>
public sealed record LiveMessage(
    string RuleId,
    MessagePriority Priority,
    string Text,
    string MessageKey,
    DateTimeOffset IssuedAt);

/// <summary>
/// One phase's suggested item, display-ready (name already resolved, not
/// just an id). ItemId (e.g. "item_tango", GSI's own naming) is kept
/// alongside the display name so the overlay can resolve an icon image
/// without RecommendationEngines needing to know anything about icons —
/// icon resolution is a UI concern, per dota-architecture.
///
/// PriorityPercent (0-100) is the LLM-explained advisor's confidence that
/// this item is the right buy right now, mirroring the win-rate-by-item
/// percentages shown on Dota's own post-game item guide — it is NOT a
/// win-rate statistic, just a relative priority signal from the same
/// structured recommendation. Null for the rule/ML-driven ItemPlan path,
/// which doesn't produce this today.
/// </summary>
public sealed record ItemPlanEntry(string PhaseLabel, string ItemDisplayName, string ItemId, int? PriorityPercent = null);

/// <summary>
/// The always-visible early/mid/late item plan — deliberately NOT part of
/// the cooldown-gated LiveMessage stream (see dota-live-coach: live
/// messages stay short single-action fragments, never a multi-item
/// plan). Built directly from NextItemRule's output every recompute, so
/// it always reflects the current state without needing its own
/// anti-spam gating — it's a persistent panel, not a transient alert.
/// Empty when NextItemRule has nothing to say (e.g. feed not usable).
/// </summary>
public sealed record ItemPlan(IReadOnlyList<ItemPlanEntry> Entries)
{
    public static readonly ItemPlan Empty = new(Array.Empty<ItemPlanEntry>());
}
