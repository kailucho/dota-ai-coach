using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines;

public sealed record RuleContext
{
    public required DateTimeOffset Now { get; init; }
    public IItemCatalog Items { get; init; } = ItemCatalog.Default;
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
