namespace DotaAiCoach.RecommendationEngines;

[Flags]
public enum ItemTag
{
    None = 0,
    Bkb = 1,
    Mobility = 2,
    Sustain = 4,
    Damage = 8,
    Utility = 16,
    AntiMagic = 32,
    AntiIllusion = 64,
    Save = 128,
    Vision = 256
}

/// <summary>
/// Typical purchase timing/role bucket, matching how Dota's own in-game
/// item guides group suggestions (Starting/Early Game/Core/Situational),
/// simplified per the user's explicit request to Early/Mid/Late +
/// Situational. Early/Mid/Late are coarse (matches
/// HeroItemPopularityTable's own early/mid/late minute buckets, see
/// docs/adr/0024) — this is a display grouping over the existing
/// ML-ranked catalog, not a new model. Situational is a separate bucket
/// for context-dependent picks (anti-magic, anti-illusion, save/utility)
/// that aren't part of a standard progression at any fixed minute.
/// </summary>
public enum ItemPhase
{
    Early,
    Mid,
    Late,
    Situational
}

/// <summary>A single phase's top-ranked, not-yet-owned item — see NextItemSelector.SelectByPhase.</summary>
public sealed record PhasedItemSuggestion(ItemPhase Phase, string ItemId);

/// <summary>
/// Item ids match GSI's own naming (item_xxx) so lookups against
/// LiveFeatureSet.OwnedItems work directly.
///
/// <see cref="UpgradesTo"/> lists item ids that, when owned, mean this
/// item's need is already satisfied — Dota consumes lower-tier
/// components into an upgraded item (e.g. Boots is consumed into Phase
/// Boots/Power Treads), so "do I still need Boots" must check for the
/// upgraded item too, not just the exact id. Without this, a candidate
/// like "item_boots" stays recommended forever after the player upgrades
/// past it, since it genuinely leaves the player's inventory.
///
/// <see cref="ExcludedByOwning"/> lists item ids that make THIS item
/// pointless to recommend once owned, even though this item is not
/// itself an upgrade of them — e.g. Power Treads and Phase Boots are
/// mutually exclusive boot upgrades (a hero can only ever equip one boot
/// item), so owning one must exclude the other from being recommended,
/// not just exclude the shared base "Boots" item.
/// </summary>
public sealed record ItemDefinition(
    string Id,
    string DisplayName,
    int Cost,
    ItemTag Tags,
    ItemPhase Phase,
    IReadOnlyList<string>? UpgradesTo = null,
    IReadOnlyList<string>? ExcludedByOwning = null)
{
    public IReadOnlyList<string> UpgradesTo { get; init; } = UpgradesTo ?? Array.Empty<string>();
    public IReadOnlyList<string> ExcludedByOwning { get; init; } = ExcludedByOwning ?? Array.Empty<string>();
}

public interface IItemCatalog
{
    bool TryGet(string itemId, out ItemDefinition definition);
    IReadOnlyList<ItemDefinition> All { get; }
}

/// <summary>
/// A small, hardcoded set of common items with current-patch costs. This
/// is a deliberate placeholder for the full candidate-generation/scoring/
/// ranking pipeline dota-item-recommender describes — see docs/ROADMAP.md.
/// Costs are current-patch values and must be re-verified on a patch
/// bump; see the inline note below.
/// </summary>
public sealed class ItemCatalog : IItemCatalog
{
    // PATCH: 7.3x — verify item costs on patch bump. This is a small,
    // hand-maintained seed set for the vertical slice's rule engine, not
    // a full item database — see DotaAiCoach.RecommendationEngines'
    // README/ROADMAP notes on the intended future replacement.
    private static readonly ItemDefinition[] Seed =
    {
        // --- Early (starting reg/consumables + first-back items) ---
        new("item_tango", "Tango", 90, ItemTag.Sustain, ItemPhase.Early),
        new("item_flask", "Healing Salve", 100, ItemTag.Sustain, ItemPhase.Early),
        new("item_clarity", "Clarity", 50, ItemTag.Sustain, ItemPhase.Early),
        new("item_faerie_fire", "Faerie Fire", 30, ItemTag.Sustain, ItemPhase.Early),
        new("item_branches", "Iron Branch", 53, ItemTag.None, ItemPhase.Early),
        new("item_circlet", "Circlet", 155, ItemTag.None, ItemPhase.Early),
        new("item_magic_stick", "Magic Stick", 200, ItemTag.Sustain, ItemPhase.Early, UpgradesTo: new[] { "item_magic_wand" }),
        new("item_magic_wand", "Magic Wand", 450, ItemTag.Sustain, ItemPhase.Early),
        new("item_wraith_band", "Wraith Band", 500, ItemTag.Damage, ItemPhase.Early),
        new("item_bracer", "Bracer", 505, ItemTag.Sustain, ItemPhase.Early),
        new("item_null_talisman", "Null Talisman", 475, ItemTag.Sustain, ItemPhase.Early),
        new("item_boots", "Boots", 500, ItemTag.Mobility, ItemPhase.Early,
            UpgradesTo: new[] { "item_power_treads", "item_phase_boots", "item_arcane_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_ring_of_basilius", "Ring of Basilius", 475, ItemTag.Sustain, ItemPhase.Early),

        // --- Mid (boots upgrades + first core pieces) ---
        new("item_power_treads", "Power Treads", 1400, ItemTag.Mobility | ItemTag.Damage, ItemPhase.Mid,
            ExcludedByOwning: new[] { "item_phase_boots", "item_arcane_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_phase_boots", "Phase Boots", 1500, ItemTag.Mobility | ItemTag.Damage, ItemPhase.Mid,
            ExcludedByOwning: new[] { "item_power_treads", "item_arcane_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_arcane_boots", "Arcane Boots", 1300, ItemTag.Utility | ItemTag.Sustain, ItemPhase.Mid,
            ExcludedByOwning: new[] { "item_power_treads", "item_phase_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_blink", "Blink Dagger", 2250, ItemTag.Mobility, ItemPhase.Mid),
        new("item_aghanims_shard", "Aghanim's Shard", 1400, ItemTag.Utility, ItemPhase.Mid),
        new("item_mekansm", "Mekansm", 1700, ItemTag.Sustain | ItemTag.Utility, ItemPhase.Mid),
        new("item_urn_of_shadows", "Urn of Shadows", 875, ItemTag.Utility, ItemPhase.Mid),
        new("item_maelstrom", "Maelstrom", 2350, ItemTag.Damage, ItemPhase.Mid),
        new("item_veil_of_discord", "Veil of Discord", 1375, ItemTag.Utility, ItemPhase.Mid),
        new("item_force_staff", "Force Staff", 2200, ItemTag.Mobility | ItemTag.Save, ItemPhase.Mid),

        // --- Late (core damage/tank/scepter items) ---
        new("item_black_king_bar", "Black King Bar", 4050, ItemTag.Bkb | ItemTag.Sustain, ItemPhase.Late),
        new("item_desolator", "Desolator", 3500, ItemTag.Damage, ItemPhase.Late),
        new("item_manta", "Manta Style", 4600, ItemTag.Mobility | ItemTag.Damage | ItemTag.AntiMagic, ItemPhase.Late),
        new("item_ultimate_scepter", "Aghanim's Scepter", 4200, ItemTag.Utility, ItemPhase.Late),
        new("item_bfury", "Battle Fury", 4100, ItemTag.Damage | ItemTag.Sustain, ItemPhase.Late),
        new("item_assault", "Assault Cuirass", 5350, ItemTag.Damage | ItemTag.Utility, ItemPhase.Late),
        new("item_sheepstick", "Scythe of Vyse", 5675, ItemTag.Utility, ItemPhase.Late),
        new("item_skadi", "Eul's Scepter", 2350, ItemTag.Utility | ItemTag.Mobility, ItemPhase.Late),
        new("item_greater_crit", "Daedalus", 5150, ItemTag.Damage, ItemPhase.Late),
        new("item_abyssal_blade", "Abyssal Blade", 6400, ItemTag.Damage | ItemTag.Save, ItemPhase.Late),

        // --- Situational (context-dependent, never a fixed-minute pick) ---
        new("item_diffusal_blade", "Diffusal Blade", 2650, ItemTag.AntiMagic | ItemTag.Damage, ItemPhase.Situational),
        new("item_orchid", "Orchid Malevolence", 3475, ItemTag.AntiMagic | ItemTag.Damage, ItemPhase.Situational),
        new("item_pipe", "Pipe of Insight", 3650, ItemTag.AntiMagic | ItemTag.Utility, ItemPhase.Situational),
        new("item_hood_of_defiance", "Hood of Defiance", 1900, ItemTag.AntiMagic | ItemTag.Sustain, ItemPhase.Situational),
        new("item_monkey_king_bar", "Monkey King Bar", 4550, ItemTag.AntiIllusion | ItemTag.Damage, ItemPhase.Situational),
        new("item_nullifier", "Nullifier", 4650, ItemTag.AntiMagic | ItemTag.Damage, ItemPhase.Situational),
        new("item_lotus_orb", "Lotus Orb", 2500, ItemTag.Save | ItemTag.Utility, ItemPhase.Situational),
        new("item_glimmer_cape", "Glimmer Cape", 1950, ItemTag.Save | ItemTag.Utility, ItemPhase.Situational),
        new("item_ghost", "Ghost Scepter", 1300, ItemTag.Save | ItemTag.AntiMagic, ItemPhase.Situational),
        new("item_gem", "Gem of True Sight", 700, ItemTag.Vision, ItemPhase.Situational)
    };

    public static readonly ItemCatalog Default = new();

    private readonly Dictionary<string, ItemDefinition> _byId =
        Seed.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string itemId, out ItemDefinition definition) => _byId.TryGetValue(itemId, out definition!);

    public IReadOnlyList<ItemDefinition> All { get; } = Seed;
}
