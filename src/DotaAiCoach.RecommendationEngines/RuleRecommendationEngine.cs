using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines;

/// <summary>
/// Runs every registered rule against the current LiveFeatureSet and
/// returns the resulting recommendations, sorted by priority then score.
/// Per dota-gsi's stale-suppression requirement, produces nothing at all
/// when the feature set is unusable (stale feed / not in progress) — a
/// rule never needs to re-check this itself.
/// </summary>
public sealed class RuleRecommendationEngine
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly TimeProvider _timeProvider;

    public RuleRecommendationEngine(IEnumerable<IRule> rules, TimeProvider? timeProvider = null)
    {
        _rules = rules.ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<Recommendation> Evaluate(LiveFeatureSet features)
    {
        if (!features.IsUsable)
        {
            return Array.Empty<Recommendation>();
        }

        var context = new RuleContext { Now = _timeProvider.GetUtcNow() };
        var results = new List<Recommendation>();

        foreach (var rule in _rules)
        {
            Recommendation? recommendation;
            try
            {
                recommendation = rule.Evaluate(features, context);
            }
            catch (Exception)
            {
                // A single misbehaving rule must not take down the others.
                // In production this would go through structured logging
                // (per docs/ARCHITECTURE.md); this slice keeps the engine
                // itself free of a logging dependency.
                continue;
            }

            if (recommendation is not null)
            {
                results.Add(recommendation);
            }
        }

        return results
            .OrderBy(r => r.Priority)
            .ThenByDescending(r => r.Score)
            .ToArray();
    }
}
