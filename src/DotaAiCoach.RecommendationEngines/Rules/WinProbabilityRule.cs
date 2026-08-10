using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Rules;

/// <summary>
/// Surfaces the LIVE_ALLOWED win-probability model's estimate (docs/adr/
/// 0022) as its own P2 informational message, entirely separate from the
/// other 4 rules' Score/Priority (per docs/ROADMAP.md's "Recommended next
/// phase" decision: supplement, don't blend). Never fires when
/// context.WinProbabilityPredictor is unwired (e.g. tests) or when
/// features.HeroId/Team/GameMinutes are unknown.
///
/// Only 3 of the model's 13 features are populated from real, currently-
/// subscribed GSI data (kills_diff_at_T, minute_T, first_blood_radiant is
/// left unknown/0.5); the rest (gold/xp advantage, towers/barracks/roshan
/// diff, hero_pick_winrate_diff, avg_rank_tier) are filled with the
/// model's documented missing-value convention (0.0), matching training-
/// time df.fillna(0.0) — a deliberate, honestly-scoped limitation (the
/// GSI subscription would need building_state/Roshan added to go
/// further; see docs/ROADMAP.md). The resulting estimate is directional,
/// not precise.
/// </summary>
public sealed class WinProbabilityRule : IRule
{
    public string RuleId => "win-probability";
    public MessagePriority Priority => MessagePriority.P2;

    public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context)
    {
        if (context.WinProbabilityPredictor is not { } predictor)
        {
            return null;
        }

        if (features.IsRadiant is not { } isRadiant || features.GameMinutes is not { } minute)
        {
            return null;
        }

        var killsDiff = (features.RadiantScore is { } r && features.DireScore is { } d)
            ? (double)(r - d)
            : 0.0;

        var input = new WinProbabilityInput
        {
            GoldAdvantageAtMinute = 0.0,
            XpAdvantageAtMinute = 0.0,
            GoldAdvantageSlope5 = 0.0,
            XpAdvantageSlope5 = 0.0,
            KillsDifferenceAtMinute = killsDiff,
            DeathsInLast5MinDifference = 0.0,
            TowersKilledDifference = 0.0,
            BarracksKilledDifference = 0.0,
            RoshanKilledDifference = 0.0,
            HeroPickWinrateDifference = 0.0,
            AverageRankTier = context.AverageRankTier ?? 0.0,
            MinuteOfMatch = minute,
            FirstBloodRadiant = null
        };

        var output = predictor.Predict(input);

        // Model output is P(Radiant win); flip to "the player's own side"
        // for a player-facing message, per dota-live-coach's own-state framing.
        var ownSideWinProbability = isRadiant
            ? output.CalibratedRadiantWinProbability
            : 1.0 - output.CalibratedRadiantWinProbability;

        var percent = (int)Math.Round(ownSideWinProbability * 100.0);
        // 5-point band so the message doesn't re-fire on every 1% fluctuation.
        var band = percent / 5;

        return new Recommendation
        {
            RuleId = RuleId,
            Priority = Priority,
            Message = $"Win chance: {percent}%.",
            MessageKey = $"win-probability:{band}",
            Score = ownSideWinProbability,
            Confidence = 0.4,
            ReasonCodes = new[] { ReasonCode.MlWinProbabilityEstimate },
            DetailText = "Estimated from the win-probability model using only kills and match minute currently available live; gold/XP/objective differences are not yet wired in."
        };
    }
}
