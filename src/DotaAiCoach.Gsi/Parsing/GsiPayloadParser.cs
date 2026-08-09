using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotaAiCoach.Gsi.Wire;

namespace DotaAiCoach.Gsi.Parsing;

public sealed record GsiParseResult(GsiPayload? Payload, bool Success, string? Error)
{
    public static GsiParseResult Ok(GsiPayload payload) => new(payload, true, null);
    public static GsiParseResult Fail(string error) => new(null, false, error);
}

public interface IGsiPayloadParser
{
    GsiParseResult Parse(string json);
}

/// <summary>
/// Deserializes a raw GSI POST body into <see cref="GsiPayload"/>. Never
/// throws — a malformed or unexpected payload returns a failed
/// <see cref="GsiParseResult"/> instead, because a crashing listener
/// causes the Dota client to back off (per the GSI configuration's
/// timeout behavior), which would make ingestion less reliable, not more
/// defensive.
/// </summary>
public sealed class GsiPayloadParser : IGsiPayloadParser
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        // matchid and some numeric fields have been observed as strings in
        // community captures; accept either representation.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public GsiParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GsiParseResult.Fail("Empty request body.");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<GsiPayload>(json, Options);
            return payload is null
                ? GsiParseResult.Fail("Deserialized payload was null.")
                : GsiParseResult.Ok(payload);
        }
        catch (JsonException ex)
        {
            return GsiParseResult.Fail($"Malformed GSI JSON: {ex.Message}");
        }
        catch (DecoderFallbackException ex)
        {
            return GsiParseResult.Fail($"Invalid encoding in GSI payload: {ex.Message}");
        }
    }
}
