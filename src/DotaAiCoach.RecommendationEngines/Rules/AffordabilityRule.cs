using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Rules;

/// <summary>
/// Reports whether the current next-item target is affordable now, close
/// to affordable, or needs more farm, with a farm-time ETA derived from
/// recent GPM (not a global/lifetime average). Uses the same
/// NextItemSelector as NextItemRule so the two rules can never disagree
/// about which item is being evaluated.
/// </summary>
public sealed class AffordabilityRule : IRule
{
    private const double CloseThreshold = 0.75;

    public string RuleId => "affordability";
    public MessagePriority Priority => MessagePriority.P1;

    public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context)
    {
        var candidates = NextItemSelector.SelectCandidates(features, 1, context.Items);
        if (candidates.Count == 0)
        {
            return null;
        }

        var itemId = candidates[0];
        if (!context.Items.TryGet(itemId, out var item) || features.TotalGold is not { } gold)
        {
            return null;
        }

        if (gold >= item.Cost)
        {
            return new Recommendation
            {
                RuleId = RuleId,
                Priority = Priority,
                Message = $"Buy {item.DisplayName}.",
                MessageKey = $"afford:{itemId}:ready",
                ItemId = itemId,
                Score = 0.9,
                Confidence = 0.7,
                ReasonCodes = new[] { ReasonCode.AffordableNow }
            };
        }

        var remaining = item.Cost - gold;

        if (gold >= item.Cost * CloseThreshold)
        {
            return new Recommendation
            {
                RuleId = RuleId,
                Priority = Priority,
                Message = $"{item.DisplayName} in {remaining}g.",
                // Bucketed to a 200g band so this doesn't re-fire on every
                // small gold tick — see LiveMessageBudget for how this
                // combines with cooldowns for anti-spam.
                MessageKey = $"afford:{itemId}:close:{remaining / 200}",
                ItemId = itemId,
                Score = 0.7,
                Confidence = 0.6,
                ReasonCodes = new[] { ReasonCode.CloseToAffording }
            };
        }

        if (features.Gpm is { } gpm && gpm > 0)
        {
            var minutesRemaining = remaining / (gpm / 60.0) / 60.0;
            return new Recommendation
            {
                RuleId = RuleId,
                Priority = Priority,
                Message = $"Farm {Math.Ceiling(minutesRemaining):0}m -> {item.DisplayName}.",
                MessageKey = $"afford:{itemId}:farm:{remaining / 200}",
                ItemId = itemId,
                Score = 0.5,
                Confidence = 0.4,
                ReasonCodes = new[] { ReasonCode.FarmUntilAffordable }
            };
        }

        // GPM unknown and far short: no fabricated ETA.
        return null;
    }
}
