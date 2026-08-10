using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines.Rules;

/// <summary>
/// Surfaces several ML-ranked (or, without ML wired, fallback-ordered)
/// candidates per purchase phase — Early/Mid/Late/Situational, per
/// ItemPhase — mirroring the grid-of-several-items-per-category style of
/// Dota's own in-game item guides, per the user's explicit request. The
/// live Message itself stays a short, single-item fragment per
/// dota-live-coach's message-style rule (no plans/paragraphs during
/// active play) — it targets the single top candidate of the earliest
/// phase that still has one, since that's the most immediately
/// actionable pick. The full multi-item, multi-phase plan is carried in
/// Alternatives and ItemPlan, for the overlay's always-on item-plan panel
/// (see OverlayViewModel.ItemPlan) rather than the cooldown-gated live
/// message stream.
/// </summary>
public sealed class NextItemRule : IRule
{
    public string RuleId => "next-item";
    public MessagePriority Priority => MessagePriority.P1;

    public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context)
    {
        // Requires a known team (features.IsRadiant), not just known
        // scores — without knowing which side is "the enemy," defaulting
        // to either team would be a fabricated guess, not a derived fact.
        var enemyTeamAheadOnKills = features.RadiantScore is { } r && features.DireScore is { } d && features.IsRadiant is { } isRadiant
            && (isRadiant ? d > r : r > d);

        var byPhase = NextItemSelector.SelectByPhase(
            features, context.Items, context.ItemRankPredictor, context.ItemPopularity, enemyTeamAheadOnKills, context.AverageRankTier);

        if (byPhase.Count == 0)
        {
            return null;
        }

        var primary = byPhase[0];
        if (!context.Items.TryGet(primary.ItemId, out var primaryItem))
        {
            return null;
        }

        var reasonCodes = new List<ReasonCode> { ReasonCode.CoreProgressionNext };
        if (features.TotalGold is { } gold && gold >= primaryItem.Cost)
        {
            reasonCodes.Add(ReasonCode.AffordableNow);
        }
        if (enemyTeamAheadOnKills)
        {
            reasonCodes.Add(ReasonCode.EnemyTeamAheadOnKills);
        }

        var alternatives = byPhase
            .Skip(1)
            .Select((suggestion, index) =>
                context.Items.TryGet(suggestion.ItemId, out var def)
                    ? new RecommendationAlternative(suggestion.ItemId, 1.0 - (index + 1) * 0.15, new[] { ReasonCode.CoreProgressionNext })
                    : null)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToArray();

        var planEntries = byPhase
            .Select(s => context.Items.TryGet(s.ItemId, out var def) ? new ItemPlanEntry(PhaseLabel(s.Phase), def.DisplayName, def.Id) : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToArray();
        var planText = string.Join(" / ", planEntries.Select(e => $"{e.PhaseLabel}: {e.ItemDisplayName}"));

        return new Recommendation
        {
            RuleId = RuleId,
            Priority = Priority,
            Message = $"Next: {primaryItem.DisplayName}.",
            MessageKey = $"next-item:{primary.ItemId}",
            ItemId = primary.ItemId,
            Score = 1.0,
            Confidence = 0.5,
            ReasonCodes = reasonCodes,
            Alternatives = alternatives,
            DetailText = enemyTeamAheadOnKills
                ? $"{planText}. The enemy team is ahead on kills, so late-game survivability/utility items are prioritized."
                : planText,
            ItemPlan = new ItemPlan(planEntries)
        };
    }

    private static string PhaseLabel(ItemPhase phase) => phase switch
    {
        ItemPhase.Early => "Early",
        ItemPhase.Mid => "Mid",
        ItemPhase.Late => "Late",
        ItemPhase.Situational => "Situational",
        _ => phase.ToString()
    };
}
