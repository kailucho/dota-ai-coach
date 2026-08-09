using System.Text.Json.Serialization;

namespace DotaAiCoach.Gsi.Wire;

/// <summary>
/// GSI sends items as an object keyed by slotN/stashN/teleport0/neutral0
/// rather than fixed properties. Modeling this as a dictionary (via
/// <see cref="GsiSlotDictionaryConverter{T}"/>) instead of 14+ fixed
/// properties means the parser doesn't break the moment Valve adds a slot.
/// Raw keys ("slot0", "stash3", "neutral0") are preserved for the
/// normalizer to interpret.
/// </summary>
[JsonConverter(typeof(GsiItemsConverter))]
public sealed record GsiItems
{
    public IReadOnlyDictionary<string, GsiItem> Slots { get; init; } = new Dictionary<string, GsiItem>();
}

public sealed record GsiItem
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("purchaser")] public int? Purchaser { get; init; }
    [JsonPropertyName("cooldown")] public int? Cooldown { get; init; }
    [JsonPropertyName("passive")] public bool? Passive { get; init; }
    [JsonPropertyName("can_cast")] public bool? CanCast { get; init; }

    // VERIFY-VIA-FIXTURE (docs/RESEARCH.md): charges are observed under
    // differing keys depending on item type. Capture all three; never
    // assume only one is populated.
    [JsonPropertyName("charges")] public int? Charges { get; init; }
    [JsonPropertyName("item_charges")] public int? ItemCharges { get; init; }
    [JsonPropertyName("ability_charges")] public int? AbilityCharges { get; init; }

    /// <summary>First non-null of the three observed charge keys.</summary>
    public int? EffectiveCharges => Charges ?? ItemCharges ?? AbilityCharges;
}

[JsonConverter(typeof(GsiAbilitiesConverter))]
public sealed record GsiAbilities
{
    public IReadOnlyDictionary<string, GsiAbility> Slots { get; init; } = new Dictionary<string, GsiAbility>();
}

public sealed record GsiAbility
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("level")] public int? Level { get; init; }
    [JsonPropertyName("can_cast")] public bool? CanCast { get; init; }
    [JsonPropertyName("passive")] public bool? Passive { get; init; }
    [JsonPropertyName("ability_active")] public bool? AbilityActive { get; init; }
    [JsonPropertyName("cooldown")] public int? Cooldown { get; init; }
    [JsonPropertyName("ultimate")] public bool? Ultimate { get; init; }
}
