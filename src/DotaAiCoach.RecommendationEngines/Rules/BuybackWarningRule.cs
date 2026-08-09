using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Rules;

/// <summary>
/// Warns when buyback is explicitly unavailable during a risky moment.
/// Only fires on an explicit false — HasBuybackAvailable being null
/// (undecidable, e.g. buyback_cost never populated) must never be treated
/// as "unavailable," since that would produce a confident-sounding P0
/// alert from data we don't actually have.
/// </summary>
public sealed class BuybackWarningRule : IRule
{
    private const int RiskMinuteThreshold = 20;
    private const double LowHealthThreshold = 0.5;

    public string RuleId => "buyback";
    public MessagePriority Priority => MessagePriority.P0;

    public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context)
    {
        if (features.HasBuybackAvailable != false)
        {
            return null;
        }

        var lateGame = features.GameMinutes is { } minutes && minutes >= RiskMinuteThreshold;
        var atRisk = features.IsAlive == false || (features.HealthFraction is { } hp && hp < LowHealthThreshold);

        if (!lateGame || !atRisk)
        {
            return null;
        }

        var reasonCodes = new List<ReasonCode> { ReasonCode.NoBuybackAvailable, ReasonCode.LateGameDeathRisk };
        if (features.BuybackCooldown is > 0)
        {
            reasonCodes.Add(ReasonCode.BuybackOnCooldown);
        }
        if (features.HealthFraction is { } hpFraction && hpFraction < LowHealthThreshold)
        {
            reasonCodes.Add(ReasonCode.LowHealthRisk);
        }

        return new Recommendation
        {
            RuleId = RuleId,
            Priority = Priority,
            Message = "No buyback.",
            MessageKey = "buyback:unavailable",
            Score = 1.0,
            Confidence = 0.8,
            ReasonCodes = reasonCodes,
            DetailText = "You currently have no buyback available and are at meaningful risk this late in the game."
        };
    }
}
