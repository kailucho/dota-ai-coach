namespace DotaAiCoach.Domain.Match;

/// <summary>
/// A single item slot (inventory, stash, teleport, or neutral). GSI sends
/// an empty slot as an object with <c>name: "empty"</c> — it is never
/// omitted from the payload. This type models that sentinel explicitly via
/// <see cref="IsEmpty"/> rather than using null, so "no item here" and
/// "we haven't heard about this slot yet" stay distinguishable.
/// </summary>
public sealed record ItemSlot
{
    public const string EmptyName = "empty";

    public string? Name { get; init; }

    /// <summary>True when this slot is unpopulated: Name is null (never observed) or the "empty" sentinel (observed and confirmed empty).</summary>
    public bool IsEmpty => Name is null or EmptyName;

    public int? Cooldown { get; init; }

    /// <summary>
    /// Resolved charge count. GSI has been observed to report charges under
    /// three different keys (charges / item_charges / ability_charges)
    /// depending on item type — see docs/RESEARCH.md. The wire-layer parser
    /// captures all three and this is the first non-null value; consumers
    /// should never assume a specific source key.
    /// </summary>
    public int? Charges { get; init; }

    public bool? Passive { get; init; }
    public bool? CanCast { get; init; }

    public static readonly ItemSlot Empty = new() { Name = EmptyName };
}
