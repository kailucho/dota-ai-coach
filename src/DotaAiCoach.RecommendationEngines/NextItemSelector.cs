using DotaAiCoach.FeatureEngine;

namespace DotaAiCoach.RecommendationEngines;

/// <summary>
/// Shared candidate-selection logic used by both NextItemRule and
/// AffordabilityRule, so the two rules can never disagree about what the
/// next item actually is. Deliberately simple: a small, patch-tagged
/// generic progression (boots -> sustain -> mobility/BKB), independent of
/// hero — a placeholder for the hero/context-aware candidate-generation
/// pipeline dota-item-recommender describes, not the intended final
/// design. See docs/ROADMAP.md.
/// </summary>
public static class NextItemSelector
{
    // Ordered by typical progression. First item in this list not already
    // owned AND not superseded by an owned upgrade (see ItemDefinition.
    // UpgradesTo) is the "next" item; the following two (also filtered)
    // are alternatives.
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
        SelectCandidates(features, count, ItemCatalog.Default);

    public static IReadOnlyList<string> SelectCandidates(LiveFeatureSet features, int count, IItemCatalog catalog)
    {
        return ProgressionOrder
            .Where(id => !IsAlreadySatisfied(id, features, catalog))
            .Take(count)
            .ToArray();
    }

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
