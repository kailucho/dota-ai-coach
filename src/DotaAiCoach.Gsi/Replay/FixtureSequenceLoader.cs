using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotaAiCoach.Gsi.Replay;

/// <summary>A single fixture file loaded for replay, in sequence order.</summary>
public sealed record FixturePayload(string FileName, int SequenceIndex, string RawJson, long? ProviderTimestamp);

/// <summary>
/// Loads a directory of hand-built or captured GSI payload fixtures for
/// use by <see cref="GsiReplaySimulator"/>, ordered by an "NN-" filename
/// prefix (e.g. "01-pregame.json", "02-game-start.json") so multi-step
/// scenarios stay self-documenting. Files without a numeric prefix fall
/// back to ordinal filename order.
/// </summary>
public static class FixtureSequenceLoader
{
    private static readonly Regex NumericPrefix = new(@"^(\d+)-", RegexOptions.Compiled);

    public static IReadOnlyList<FixturePayload> Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Fixture directory not found: {directory}");
        }

        var files = Directory.GetFiles(directory, "*.json")
            .OrderBy(GetSortKey, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException($"Fixture directory contains no .json files: {directory}");
        }

        var result = new List<FixturePayload>(files.Length);
        for (var i = 0; i < files.Length; i++)
        {
            var raw = File.ReadAllText(files[i]);
            result.Add(new FixturePayload(Path.GetFileName(files[i]), i, raw, TryExtractProviderTimestamp(raw)));
        }

        return result;
    }

    private static string GetSortKey(string path)
    {
        var name = Path.GetFileName(path);
        var match = NumericPrefix.Match(name);
        // Zero-pad so numeric prefixes sort correctly as strings ("02-" before "10-").
        return match.Success ? match.Groups[1].Value.PadLeft(10, '0') + name : "9999999999" + name;
    }

    private static long? TryExtractProviderTimestamp(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("provider", out var provider) &&
                provider.TryGetProperty("timestamp", out var ts) &&
                ts.TryGetInt64(out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // Malformed fixtures are valid test input (e.g. the "malformed" case);
            // the simulator treats the payload as opaque either way.
        }

        return null;
    }
}
