namespace DotaAiCoach.RecommendationEngines;

[Flags]
public enum ItemTag
{
    None = 0,
    Bkb = 1,
    Mobility = 2,
    Sustain = 4,
    Damage = 8,
    Utility = 16
}

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
        new("item_tango", "Tango", 90, ItemTag.Sustain),
        new("item_magic_stick", "Magic Stick", 200, ItemTag.Sustain, UpgradesTo: new[] { "item_magic_wand" }),
        new("item_boots", "Boots", 500, ItemTag.Mobility,
            UpgradesTo: new[] { "item_power_treads", "item_phase_boots", "item_arcane_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_power_treads", "Power Treads", 1400, ItemTag.Mobility | ItemTag.Damage,
            ExcludedByOwning: new[] { "item_phase_boots", "item_arcane_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_phase_boots", "Phase Boots", 1500, ItemTag.Mobility | ItemTag.Damage,
            ExcludedByOwning: new[] { "item_power_treads", "item_arcane_boots", "item_tranquil_boots", "item_guardian_greaves" }),
        new("item_magic_wand", "Magic Wand", 450, ItemTag.Sustain),
        new("item_wraith_band", "Wraith Band", 500, ItemTag.Damage),
        new("item_bracer", "Bracer", 505, ItemTag.Sustain),
        new("item_blink", "Blink Dagger", 2250, ItemTag.Mobility),
        new("item_black_king_bar", "Black King Bar", 4050, ItemTag.Bkb | ItemTag.Sustain),
        new("item_desolator", "Desolator", 3500, ItemTag.Damage),
        new("item_manta", "Manta Style", 4600, ItemTag.Mobility | ItemTag.Damage),
        new("item_aghanims_shard", "Aghanim's Shard", 1400, ItemTag.Utility),
        new("item_ultimate_scepter", "Aghanim's Scepter", 4200, ItemTag.Utility),
        new("item_bfury", "Battle Fury", 4100, ItemTag.Damage | ItemTag.Sustain)
    };

    public static readonly ItemCatalog Default = new();

    private readonly Dictionary<string, ItemDefinition> _byId =
        Seed.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string itemId, out ItemDefinition definition) => _byId.TryGetValue(itemId, out definition!);

    public IReadOnlyList<ItemDefinition> All { get; } = Seed;
}
