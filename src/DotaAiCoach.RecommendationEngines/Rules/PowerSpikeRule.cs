using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Rules;

/// <summary>
/// Flags an active/imminent power-spike window: a level threshold just
/// crossed (6/11/16/18, tied to ultimate availability/refresh points), or
/// a spike-defining item (Blink, BKB) just purchased.
/// </summary>
public sealed class PowerSpikeRule : IRule
{
    private static readonly int[] LevelThresholds = { 6, 11, 16, 18 };
    private static readonly string[] SpikeItems = { "item_blink", "item_black_king_bar" };

    public string RuleId => "power-spike";
    public MessagePriority Priority => MessagePriority.P2;

    public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context)
    {
        var leveledToThreshold = features.RecentEvents
            .OfType<HeroLeveledUp>()
            .Any(e => LevelThresholds.Contains(e.ToLevel));

        var purchasedSpikeItem = features.RecentEvents
            .OfType<ItemPurchased>()
            .Any(e => SpikeItems.Contains(e.ItemName, StringComparer.OrdinalIgnoreCase));

        if (!leveledToThreshold && !purchasedSpikeItem)
        {
            return null;
        }

        var reasonCodes = new List<ReasonCode>();
        if (leveledToThreshold)
        {
            reasonCodes.Add(ReasonCode.LevelThresholdReached);
        }
        if (purchasedSpikeItem)
        {
            reasonCodes.Add(ReasonCode.ItemThresholdReached);
            reasonCodes.Add(ReasonCode.MatchesPowerSpikeWindow);
        }

        return new Recommendation
        {
            RuleId = RuleId,
            Priority = Priority,
            Message = "Power spike ready.",
            MessageKey = $"power-spike:{features.HeroLevel}:{purchasedSpikeItem}",
            Score = 0.6,
            Confidence = 0.5,
            ReasonCodes = reasonCodes
        };
    }
}
