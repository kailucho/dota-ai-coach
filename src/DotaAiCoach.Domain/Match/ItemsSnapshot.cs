using DotaAiCoach.FairPlay;

namespace DotaAiCoach.Domain.Match;

/// <summary>
/// The local player's items, normalized from GSI's <c>items</c> section.
/// <see cref="Inventory"/>, <see cref="Stash"/>, and <see cref="Neutral"/>
/// are always the fixed slot count confirmed against a real captured
/// payload (see docs/RESEARCH.md) — empty slots are represented as
/// <see cref="ItemSlot.Empty"/>, never omitted from the list, matching
/// how GSI itself never omits an empty slot from the payload.
///
/// Slot counts were corrected from an earlier assumption of 6 inventory
/// slots after a live-client capture showed slot0 through slot8 (9
/// slots) actually present — the earlier 6-slot model was silently
/// truncating real inventory data. Verify these counts again on a future
/// major patch, since Valve has changed inventory layout before.
/// </summary>
[FairPlaySource(DataSourceKind.Gsi, FairPlayClassification.LiveAllowed,
    Visibility = "GSI items section — own inventory/stash/teleport/neutral slots, shown in the player's own HUD.",
    Availability = "Whenever a GSI feed is connected and the player's hero is loaded.",
    Justification = "GSI in playing mode only exposes the local player's own inventory.")]
public sealed record ItemsSnapshot
{
    private static IReadOnlyList<ItemSlot> EmptySlots(int count) =>
        Enumerable.Repeat(ItemSlot.Empty, count).ToArray();

    /// <summary>Main inventory, slot0 through slot8 — 9 slots, confirmed against a live capture.</summary>
    public IReadOnlyList<ItemSlot> Inventory { get; init; } = EmptySlots(9);

    /// <summary>Backpack/stash, stash0 through stash5 — 6 slots.</summary>
    public IReadOnlyList<ItemSlot> Stash { get; init; } = EmptySlots(6);

    public ItemSlot? Teleport { get; init; }

    /// <summary>Active neutral item slots, neutral0/neutral1 — 2 slots (previously assumed to be 1).</summary>
    public IReadOnlyList<ItemSlot> Neutral { get; init; } = EmptySlots(2);

    /// <summary>
    /// "preserved_neutralN" slots observed in a live capture (indices 6-10)
    /// — believed to represent neutral item tiers held in reserve/locked
    /// from earlier in the game, but this is not independently confirmed
    /// against documentation (no official GSI schema exists). Kept
    /// separate from <see cref="Neutral"/> since GSI itself distinguishes
    /// the key prefix; do not assume semantics beyond "some neutral-
    /// related slot state" without further verification.
    /// </summary>
    public IReadOnlyList<ItemSlot> PreservedNeutral { get; init; } = EmptySlots(5);

    /// <summary>Distinct, non-empty item names across inventory, stash, and neutral slots.</summary>
    public IEnumerable<string> OwnedItemNames() =>
        Inventory.Concat(Stash).Concat(Neutral)
            .Where(slot => !slot.IsEmpty && slot.Name is not null)
            .Select(slot => slot.Name!)
            .Distinct();

    public bool Owns(string itemName) =>
        OwnedItemNames().Contains(itemName, StringComparer.OrdinalIgnoreCase);

    public static readonly ItemsSnapshot Empty = new();
}
