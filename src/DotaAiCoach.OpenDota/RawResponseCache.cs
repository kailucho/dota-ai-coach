using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota;

/// <summary>
/// Disk cache of raw OpenDota responses, keyed by endpoint. Per dota-data:
/// a RAW response is stored verbatim, never mutated, with fetch/source
/// metadata attached. Writes are atomic (temp file + File.Move overwrite)
/// so a crash mid-write cannot leave a truncated cache file.
/// </summary>
public sealed class RawResponseCache
{
    private readonly string _directory;
    private readonly TimeProvider _timeProvider;

    public RawResponseCache(string directory, TimeProvider? timeProvider = null)
    {
        _directory = directory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CachedEntry?> ReadAsync(string endpoint, CancellationToken ct)
    {
        var path = PathFor(endpoint);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<CachedEntry>(json);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task WriteAsync(string endpoint, string body, int? statusCode, CancellationToken ct) =>
        WriteAsync(endpoint, body, statusCode, isParsed: false, ct);

    /// <summary>
    /// isParsed marks a match-detail response as OpenDota's post-parse
    /// shape (see docs/adr/0016) — set explicitly by the caller, never
    /// inferred by sniffing the JSON shape at read time, since shape-
    /// sniffing heuristics drift as OpenDota's schema evolves. A parsed
    /// match's content never changes once written, so callers may pass
    /// TimeSpan.MaxValue as the effective TTL when reading it back.
    /// </summary>
    public async Task WriteAsync(string endpoint, string body, int? statusCode, bool isParsed, CancellationToken ct)
    {
        Directory.CreateDirectory(_directory);

        var entry = new CachedEntry(endpoint, _timeProvider.GetUtcNow(), statusCode, "opendota", 1, body, isParsed);
        var json = JsonSerializer.Serialize(entry);

        var path = PathFor(endpoint);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    private string PathFor(string endpoint)
    {
        var sanitized = new string(endpoint.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var hash = Math.Abs(endpoint.GetHashCode()).ToString("x8");
        return Path.Combine(_directory, $"{sanitized}_{hash}.json");
    }

    public sealed record CachedEntry(
        [property: JsonPropertyName("endpoint")] string Endpoint,
        [property: JsonPropertyName("fetchedAtUtc")] DateTimeOffset FetchedAtUtc,
        [property: JsonPropertyName("statusCode")] int? StatusCode,
        [property: JsonPropertyName("sourceName")] string SourceName,
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("isParsed")] bool IsParsed = false);
}
