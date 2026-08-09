using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotaAiCoach.Gsi.Wire;

/// <summary>
/// Reads a JSON object whose members are all slot entries directly into a
/// dictionary keyed by the raw property name (e.g. items.slot0,
/// items.stash3, abilities.ability0) — GSI has no wrapper object around
/// these slots, so the converter targets the containing type itself
/// (<see cref="GsiItems"/>/<see cref="GsiAbilities"/>), not a nested
/// property. Defensive against a stray non-object member appearing in the
/// section — such a member is skipped rather than throwing, consistent
/// with the parser's "never throw on malformed input" contract.
/// </summary>
internal static class GsiSlotDictionaryReader
{
    public static IReadOnlyDictionary<string, T> ReadSlots<T>(
        ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, T>();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return result;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = property.Value.Deserialize<T>(options);
            if (value is not null)
            {
                result[property.Name] = value;
            }
        }

        return result;
    }

    public static void WriteSlots<T>(
        Utf8JsonWriter writer, IReadOnlyDictionary<string, T> slots, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, item) in slots)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, item, options);
        }
        writer.WriteEndObject();
    }
}

public sealed class GsiItemsConverter : JsonConverter<GsiItems>
{
    public override GsiItems Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new() { Slots = GsiSlotDictionaryReader.ReadSlots<GsiItem>(ref reader, options) };

    public override void Write(Utf8JsonWriter writer, GsiItems value, JsonSerializerOptions options) =>
        GsiSlotDictionaryReader.WriteSlots(writer, value.Slots, options);
}

public sealed class GsiAbilitiesConverter : JsonConverter<GsiAbilities>
{
    public override GsiAbilities Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new() { Slots = GsiSlotDictionaryReader.ReadSlots<GsiAbility>(ref reader, options) };

    public override void Write(Utf8JsonWriter writer, GsiAbilities value, JsonSerializerOptions options) =>
        GsiSlotDictionaryReader.WriteSlots(writer, value.Slots, options);
}
