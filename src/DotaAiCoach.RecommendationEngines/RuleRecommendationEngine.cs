using DotaAiCoach.Domain.MlInference;
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
    private readonly IItemCatalog _items;
    private readonly IItemRankPredictor? _itemRankPredictor;
    private readonly IHeroItemPopularityLookup? _itemPopularity;
    private readonly IWinProbabilityPredictor? _winProbabilityPredictor;
    private int? _averageRankTier;

    public RuleRecommendationEngine(
        IEnumerable<IRule> rules,
        TimeProvider? timeProvider = null,
        IItemCatalog? items = null,
        IItemRankPredictor? itemRankPredictor = null,
        IHeroItemPopularityLookup? itemPopularity = null,
        IWinProbabilityPredictor? winProbabilityPredictor = null)
    {
        _rules = rules.ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _items = items ?? ItemCatalog.Default;
        _itemRankPredictor = itemRankPredictor;
        _itemPopularity = itemPopularity;
        _winProbabilityPredictor = winProbabilityPredictor;
    }

    /// <summary>
    /// Feeds the player's average rank tier (PUBLIC_PREFLIGHT, from the
    /// separate player-profile lookup, never from GSI) into subsequent
    /// Evaluate() calls' RuleContext. Mutable rather than constructor-only
    /// because it becomes known asynchronously, after this engine already
    /// exists (see ICoachSession.SetAverageRankTier / docs/ROADMAP.md).
    /// </summary>
    public void SetAverageRankTier(int? rankTier) => _averageRankTier = rankTier;

    public IReadOnlyList<Recommendation> Evaluate(LiveFeatureSet features)
    {
        if (!features.IsUsable)
        {
            return Array.Empty<Recommendation>();
        }

        var context = new RuleContext
        {
            Now = _timeProvider.GetUtcNow(),
            Items = _items,
            ItemRankPredictor = _itemRankPredictor,
            ItemPopularity = _itemPopularity,
            WinProbabilityPredictor = _winProbabilityPredictor,
            AverageRankTier = _averageRankTier
        };
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
