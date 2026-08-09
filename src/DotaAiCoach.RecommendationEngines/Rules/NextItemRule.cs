using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Rules;

/// <summary>Surfaces the current top item-progression target and its alternatives.</summary>
public sealed class NextItemRule : IRule
{
    public string RuleId => "next-item";
    public MessagePriority Priority => MessagePriority.P1;

    public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context)
    {
        var candidates = NextItemSelector.SelectCandidates(features, 3, context.Items);
        if (candidates.Count == 0)
        {
            return null;
        }

        var topId = candidates[0];
        if (!context.Items.TryGet(topId, out var topItem))
        {
            return null;
        }

        var reasonCodes = new List<ReasonCode> { ReasonCode.CoreProgressionNext };
        if (features.TotalGold is { } gold && gold >= topItem.Cost)
        {
            reasonCodes.Add(ReasonCode.AffordableNow);
        }

        var alternatives = candidates
            .Skip(1)
            .Select((id, index) =>
                context.Items.TryGet(id, out var def)
                    ? new RecommendationAlternative(id, 1.0 - (index + 1) * 0.15, new[] { ReasonCode.CoreProgressionNext })
                    : null)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToArray();

        return new Recommendation
        {
            RuleId = RuleId,
            Priority = Priority,
            Message = $"Next: {topItem.DisplayName}.",
            MessageKey = $"next-item:{topId}",
            ItemId = topId,
            Score = 1.0,
            Confidence = 0.5,
            ReasonCodes = reasonCodes,
            Alternatives = alternatives,
            DetailText = $"{topItem.DisplayName} is the next item in your current progression, costing {topItem.Cost} gold."
        };
    }
}
