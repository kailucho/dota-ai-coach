using System.Text.Json;

namespace DotaAiCoach.OpenDota;

public sealed record ParseProgress(int Completed, int Total, long CurrentMatchId);

/// <summary>
/// Drives OpenDota's async parse flow (POST /request/{id} -> poll GET
/// /request/{jobId} -> re-fetch GET /matches/{id}) for a bounded set of
/// matches, per docs/adr/0016: parsing is expensive, so this is only ever
/// invoked from an explicit user-triggered Deep DNA Refresh, never from a
/// normal profile fetch. Reuses the same rate-limited IOpenDotaClient as
/// every other call in this project — no separate budget.
/// </summary>
public sealed class ParsedMatchRequester
{
    private readonly IOpenDotaClient _client;
    private readonly CachingOpenDotaClient _cachingClient;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxPollAttempts;
    private readonly TimeSpan _pollInterval;

    public ParsedMatchRequester(
        IOpenDotaClient client,
        CachingOpenDotaClient cachingClient,
        TimeProvider? timeProvider = null,
        int maxPollAttempts = 10,
        TimeSpan? pollInterval = null)
    {
        _client = client;
        _cachingClient = cachingClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxPollAttempts = maxPollAttempts;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(3);
    }

    /// <summary>
    /// Parses each match not already cached as parsed, up to matchIds.Count.
    /// A match that fails or times out is simply skipped — partial success
    /// is fine, since DotaDnaBuilder degrades gracefully on fewer samples.
    /// </summary>
    public async Task<int> ParseMatchesAsync(IReadOnlyList<long> matchIds, IProgress<ParseProgress>? progress, CancellationToken ct)
    {
        var parsedCount = 0;

        for (var i = 0; i < matchIds.Count; i++)
        {
            var matchId = matchIds[i];
            var endpoint = $"/matches/{matchId}";

            progress?.Report(new ParseProgress(i, matchIds.Count, matchId));

            if (await _cachingClient.HasParsedMatchCachedAsync(endpoint, ct).ConfigureAwait(false))
            {
                parsedCount++;
                continue;
            }

            if (await TryParseOneAsync(matchId, endpoint, ct).ConfigureAwait(false))
            {
                parsedCount++;
            }
        }

        progress?.Report(new ParseProgress(matchIds.Count, matchIds.Count, 0));
        return parsedCount;
    }

    private async Task<bool> TryParseOneAsync(long matchId, string endpoint, CancellationToken ct)
    {
        var requestResponse = await _client.PostAsync($"/request/{matchId}", ct).ConfigureAwait(false);
        var jobId = TryExtractJobId(requestResponse.RawJson);
        if (jobId is null)
        {
            return false;
        }

        for (var attempt = 0; attempt < _maxPollAttempts; attempt++)
        {
            await Task.Delay(_pollInterval, ct).ConfigureAwait(false);

            var jobResponse = await _client.GetAsync($"/request/{jobId}", ct).ConfigureAwait(false);
            if (IsJobComplete(jobResponse.RawJson))
            {
                break;
            }

            if (attempt == _maxPollAttempts - 1)
            {
                // Never wait forever for a job that never completes.
                return false;
            }
        }

        var matchResponse = await _client.GetAsync(endpoint, ct).ConfigureAwait(false);
        if (matchResponse.RawJson is null)
        {
            return false;
        }

        await _cachingClient.CacheParsedMatchAsync(endpoint, matchResponse.RawJson, ct).ConfigureAwait(false);
        return true;
    }

    private static long? TryExtractJobId(string? rawJson)
    {
        if (rawJson is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("job", out var job) && job.TryGetProperty("jobId", out var jobId))
            {
                return jobId.GetInt64();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool IsJobComplete(string? rawJson)
    {
        // OpenDota's job-status endpoint returns "null" once the job has
        // left the queue (completed or expired) — a non-null body means
        // it is still pending.
        return string.IsNullOrWhiteSpace(rawJson) || rawJson.Trim() == "null";
    }
}
