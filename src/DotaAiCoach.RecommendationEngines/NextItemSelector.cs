using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines;

/// <summary>
/// Shared candidate-selection logic used by both NextItemRule and
/// AffordabilityRule, so the two rules can never disagree about what the
/// next item actually is.
///
/// When an IItemRankPredictor and IHeroItemPopularityLookup are supplied
/// (via RuleContext), candidates are ranked by the trained item-ranker
/// model (docs/adr/0020/0023/0024) over the full ItemCatalog. When either
/// is missing (e.g. HeroId unknown, or ML not wired), falls back to the
/// original small hand-ordered progression (boots -> sustain ->
/// mobility/BKB) — a placeholder for hero/context-aware ranking per
/// dota-item-recommender, kept only as the no-ML fallback now. See
/// docs/ROADMAP.md.
/// </summary>
public static class NextItemSelector
{
    // Fallback progression, used only when ML ranking is unavailable.
    // First item in this list not already owned AND not superseded by an
    // owned upgrade (see ItemDefinition.UpgradesTo) is the "next" item;
    // the following two (also filtered) are alternatives.
    private static readonly string[] ProgressionOrder =
    {
        "item_boots",
        "item_magic_wand",
        "item_bracer",
        "item_phase_boots",
        "item_power_treads",
        "item_blink",
        "item_black_king_bar",
        "item_desolator",
        "item_manta",
        "item_bfury"
    };

    public static IReadOnlyList<string> SelectCandidates(LiveFeatureSet features, int count) =>
        SelectCandidates(features, count, ItemCatalog.Default, itemRankPredictor: null, popularity: null);

    public static IReadOnlyList<string> SelectCandidates(LiveFeatureSet features, int count, IItemCatalog catalog) =>
        SelectCandidates(features, count, catalog, itemRankPredictor: null, popularity: null);

    public static IReadOnlyList<string> SelectCandidates(
        LiveFeatureSet features,
        int count,
        IItemCatalog catalog,
        IItemRankPredictor? itemRankPredictor,
        IHeroItemPopularityLookup? popularity)
    {
        if (itemRankPredictor is not null && popularity is not null
            && features.HeroId is { } heroId && features.GameMinutes is { } minute)
        {
            return SelectCandidatesByRank(features, count, catalog, itemRankPredictor, popularity, heroId, minute);
        }

        return ProgressionOrder
            .Where(id => !IsAlreadySatisfied(id, features, catalog))
            .Take(count)
            .ToArray();
    }

    private static IReadOnlyList<string> SelectCandidatesByRank(
        LiveFeatureSet features,
        int count,
        IItemCatalog catalog,
        IItemRankPredictor itemRankPredictor,
        IHeroItemPopularityLookup popularity,
        int heroId,
        double minute)
    {
        return RankCandidates(features, catalog, itemRankPredictor, popularity, heroId, minute, catalog.All, itemRankPredictor.AverageRankTierFill)
            .Take(count)
            .Select(x => x.Id)
            .ToArray();
    }

    /// <summary>Every phase, in the order guides conventionally present them.</summary>
    private static readonly ItemPhase[] AllPhases = { ItemPhase.Early, ItemPhase.Mid, ItemPhase.Late, ItemPhase.Situational };

    /// <summary>Max candidates shown per phase — matches how many items Dota's own in-game item guides typically show per category (grid of 4-6).</summary>
    private const int MaxPerPhase = 6;

    /// <summary>
    /// ML-ranked candidates grouped by phase (Early/Mid/Late/Situational —
    /// see ItemPhase), up to MaxPerPhase per phase, per the user's
    /// explicit request for a full build-guide-style grid (like Dota's
    /// own in-game item guides) rather than a single top pick per phase.
    /// Falls back to SelectCandidates' flat non-ML progression, split into
    /// phases, when ML is unavailable — same fallback discipline as the
    /// flat method.
    /// </summary>
    public static IReadOnlyList<PhasedItemSuggestion> SelectByPhase(
        LiveFeatureSet features,
        IItemCatalog catalog,
        IItemRankPredictor? itemRankPredictor,
        IHeroItemPopularityLookup? popularity,
        bool enemyTeamAheadOnKills,
        int? averageRankTier = null)
    {
        if (itemRankPredictor is not null && popularity is not null
            && features.HeroId is { } heroId && features.GameMinutes is { } minute)
        {
            var rankTier = averageRankTier ?? itemRankPredictor.AverageRankTierFill;
            var ranked = RankCandidates(features, catalog, itemRankPredictor, popularity, heroId, minute, catalog.All, rankTier, enemyTeamAheadOnKills);
            return PickTopPerPhase(ranked, catalog);
        }

        var fallbackRanked = ProgressionOrder
            .Where(id => !IsAlreadySatisfied(id, features, catalog))
            .Select((id, index) => (Id: id, Score: -(double)index)); // preserve fallback's original order
        return PickTopPerPhase(fallbackRanked, catalog);
    }

    private static IReadOnlyList<PhasedItemSuggestion> PickTopPerPhase(IEnumerable<(string Id, double Score)> rankedCandidates, IItemCatalog catalog)
    {
        var byPhase = rankedCandidates
            .Select(c => catalog.TryGet(c.Id, out var def) ? (c.Id, c.Score, Phase: (ItemPhase?)def.Phase) : (c.Id, c.Score, Phase: null))
            .Where(c => c.Phase is not null)
            .GroupBy(c => c.Phase!.Value);

        var result = new List<PhasedItemSuggestion>();
        foreach (var phase in AllPhases)
        {
            var candidatesInPhase = byPhase.FirstOrDefault(g => g.Key == phase)
                ?.OrderByDescending(c => c.Score)
                ?? Enumerable.Empty<(string Id, double Score, ItemPhase? Phase)>();

            // Mutual exclusion applies across the candidates being picked
            // here too, not just against items the player already owns
            // (that part is IsAlreadySatisfied's job, upstream). Without
            // this, e.g. Power Treads and Phase Boots -- mutually
            // exclusive boot upgrades neither the player owns yet -- could
            // both appear in the same phase's suggestions, which is never
            // a real choice a player can make (buying one locks out the
            // other). Greedily accepts the highest-scored candidate first,
            // then skips anything it excludes.
            var chosenInPhase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidatesInPhase)
            {
                if (chosenInPhase.Count >= MaxPerPhase)
                {
                    break;
                }

                if (ExcludesAnyChosen(candidate.Id, chosenInPhase, catalog))
                {
                    continue;
                }

                chosenInPhase.Add(candidate.Id);
                result.Add(new PhasedItemSuggestion(phase, candidate.Id));
            }
        }

        return result;
    }

    /// <summary>
    /// True when candidateId is mutually exclusive with anything already
    /// chosen for this phase — checked both directions (candidate excludes
    /// a chosen item, or a chosen item excludes candidate), since
    /// ExcludedByOwning is only declared on one side of each pair in the
    /// catalog (e.g. Power Treads lists Phase Boots, but Phase Boots'
    /// own entry also separately lists Power Treads back — still worth
    /// checking both ways defensively rather than relying on every pair
    /// being declared symmetrically).
    /// </summary>
    private static bool ExcludesAnyChosen(string candidateId, HashSet<string> chosenInPhase, IItemCatalog catalog)
    {
        if (!catalog.TryGet(candidateId, out var candidateDef))
        {
            return false;
        }

        if (candidateDef.ExcludedByOwning.Any(chosenInPhase.Contains))
        {
            return true;
        }

        foreach (var chosenId in chosenInPhase)
        {
            if (catalog.TryGet(chosenId, out var chosenDef) && chosenDef.ExcludedByOwning.Contains(candidateId, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string Id, double Score)> RankCandidates(
        LiveFeatureSet features,
        IItemCatalog catalog,
        IItemRankPredictor itemRankPredictor,
        IHeroItemPopularityLookup popularity,
        int heroId,
        double minute,
        IReadOnlyList<ItemDefinition> candidatePool,
        double averageRankTier,
        bool enemyTeamAheadOnKills = false)
    {
        var minuteBucket = MinuteBucket(minute);
        var ownedBeforeCount = features.OwnedItemCount;

        return candidatePool
            .Where(item => !IsAlreadySatisfied(item.Id, features, catalog))
            .Select(item =>
            {
                var input = new ItemRankInput
                {
                    MinuteOfMatch = minute,
                    ItemsOwnedBeforeCount = ownedBeforeCount,
                    AverageRankTier = averageRankTier,
                    ItemPopularityForHero = popularity.HeroPopularity(heroId, item.Id),
                    ItemPopularityForHeroMinuteBucket = popularity.HeroMinuteBucketPopularity(heroId, minuteBucket, item.Id),
                    ItemGlobalPopularity = popularity.GlobalPopularity(item.Id)
                };
                var score = itemRankPredictor.Predict(input).RankScore;

                // Threat boost: when the enemy team is pulling ahead on
                // kills (features.RadiantScore/DireScore, the only
                // legitimately GSI-available signal about the enemy team
                // right now — see docs/ROADMAP.md), nudge
                // survivability/utility items up so the always-shown
                // late-phase suggestion leans defensive rather than
                // purely damage-maximizing. Deliberately a small,
                // additive nudge (not a full re-rank) since this is a
                // team-level proxy, not knowledge of which enemy hero is
                // actually the threat.
                if (enemyTeamAheadOnKills && (item.Tags & (ItemTag.Bkb | ItemTag.Utility)) != ItemTag.None)
                {
                    score += ThreatBoost;
                }

                return (item.Id, score);
            })
            .OrderByDescending(x => x.Item2);
    }

    private const double ThreatBoost = 0.25;

    /// <summary>Minute bucket boundaries mirror item_ranker.py's _minute_bucket / HeroItemPopularityTable.MinuteBucket exactly.</summary>
    private static string MinuteBucket(double minute) =>
        minute < 15 ? "early" : minute < 30 ? "mid" : "late";

    /// <summary>
    /// True when the player owns this exact item, owns an item that this
    /// catalog entry lists as an upgrade path (per ItemDefinition.
    /// UpgradesTo — e.g. owning Phase Boots satisfies the "need" for
    /// Boots, since Dota consumed the base Boots into it), or owns an
    /// item that mutually excludes this one (per ItemDefinition.
    /// ExcludedByOwning — e.g. owning Phase Boots means Power Treads is
    /// no longer a sensible recommendation, since a hero can only equip
    /// one boot upgrade at a time, even though neither is "an upgrade of"
    /// the other).
    /// </summary>
    private static bool IsAlreadySatisfied(string itemId, LiveFeatureSet features, IItemCatalog catalog)
    {
        if (features.OwnedItems.Contains(itemId))
        {
            return true;
        }

        if (!catalog.TryGet(itemId, out var definition))
        {
            return false;
        }

        return definition.UpgradesTo.Any(features.OwnedItems.Contains)
            || definition.ExcludedByOwning.Any(features.OwnedItems.Contains);
    }
}
