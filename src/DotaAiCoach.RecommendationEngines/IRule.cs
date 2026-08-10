using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines;

public sealed record RuleContext
{
    public required DateTimeOffset Now { get; init; }
    public IItemCatalog Items { get; init; } = ItemCatalog.Default;

    /// <summary>
    /// Optional ML predictors, wired by Host (the only layer allowed to
    /// reference DotaAiCoach.MlInference). Null when ML is not wired
    /// (e.g. tests) — rules must fall back to their non-ML behavior
    /// rather than throw. See docs/adr/0023, docs/ROADMAP.md.
    /// </summary>
    public IItemRankPredictor? ItemRankPredictor { get; init; }
    public IHeroItemPopularityLookup? ItemPopularity { get; init; }
    public IWinProbabilityPredictor? WinProbabilityPredictor { get; init; }

    /// <summary>
    /// The player's average rank tier (PUBLIC_PREFLIGHT, sourced from the
    /// separate player-profile/OpenDota lookup, never from GSI) for
    /// WinProbabilityInput.AverageRankTier. Null when unknown — the rule
    /// fills 0.0, matching the model's documented missing-value
    /// convention, rather than fabricating a value.
    /// </summary>
    public int? AverageRankTier { get; init; }
}

/// <summary>
/// A single, independently-testable recommendation rule. Must be pure
/// (no I/O, no side effects) and must not throw — RuleRecommendationEngine
/// isolates and logs per-rule exceptions so one bad rule cannot take down
/// the others.
/// </summary>
public interface IRule
{
    string RuleId { get; }
    MessagePriority Priority { get; }

    /// <summary>Return null when the rule has nothing to say for this state.</summary>
    Recommendation? Evaluate(LiveFeatureSet features, RuleContext context);
}
