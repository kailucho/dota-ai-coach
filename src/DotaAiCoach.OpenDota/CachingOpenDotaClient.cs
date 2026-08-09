using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.OpenDota;

/// <summary>
/// Decorator: serves a cached response within its TTL without a network
/// call; on network failure, falls back to a stale cache entry and marks
/// the result as such — this is the offline-first behavior. A fresh
/// network success always refreshes the cache.
/// </summary>
public sealed class CachingOpenDotaClient : IOpenDotaClient
{
    private readonly IOpenDotaClient? _inner;
    private readonly RawResponseCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly bool _offlineOnly;

    public CachingOpenDotaClient(IOpenDotaClient? inner, RawResponseCache cache, bool offlineOnly, TimeProvider? timeProvider = null)
    {
        _inner = inner;
        _cache = cache;
        _offlineOnly = offlineOnly;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>POST requests (parse triggers) are never cached or served offline — they only make sense as a live call.</summary>
    public Task<OpenDotaResponse> PostAsync(string relativePath, CancellationToken ct)
    {
        if (_offlineOnly || _inner is null)
        {
            return Task.FromResult(new OpenDotaResponse { FetchedAtUtc = _timeProvider.GetUtcNow(), StatusCode = null, RawJson = null });
        }

        return _inner.PostAsync(relativePath, ct);
    }

    public async Task<OpenDotaResponse> GetAsync(string relativePath, CancellationToken ct) =>
        (await GetWithAvailabilityAsync(relativePath, TimeSpan.Zero, ct).ConfigureAwait(false)).Response;

    public async Task<(OpenDotaResponse Response, ProfileAvailability? StaleAvailability)> GetWithAvailabilityAsync(
        string relativePath, TimeSpan ttl, CancellationToken ct)
    {
        var cached = await _cache.ReadAsync(relativePath, ct).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();

        if (cached is not null && now - cached.FetchedAtUtc < ttl)
        {
            return (ToResponse(cached, servedFromCache: true), null);
        }

        if (_offlineOnly || _inner is null)
        {
            if (cached is not null)
            {
                return (ToResponse(cached, servedFromCache: true), ProfileAvailability.CacheOnlyStale);
            }

            return (new OpenDotaResponse { FetchedAtUtc = now, StatusCode = null, RawJson = null }, ProfileAvailability.Unknown);
        }

        var live = await _inner.GetAsync(relativePath, ct).ConfigureAwait(false);

        if (live.RawJson is not null)
        {
            await _cache.WriteAsync(relativePath, live.RawJson, live.StatusCode, ct).ConfigureAwait(false);
            return (live, null);
        }

        if (cached is not null)
        {
            return (ToResponse(cached, servedFromCache: true), ProfileAvailability.CacheOnlyStale);
        }

        return (live, ProfileAvailability.FetchFailed);
    }

    /// <summary>
    /// True if a parsed (post-POST /request) response for this match is
    /// already cached — checked before spending rate-limit budget on a
    /// redundant parse request. Parsed match detail never changes once
    /// written, so no TTL is applied here.
    /// </summary>
    public async Task<bool> HasParsedMatchCachedAsync(string matchDetailEndpoint, CancellationToken ct)
    {
        var cached = await _cache.ReadAsync(matchDetailEndpoint, ct).ConfigureAwait(false);
        return cached is { IsParsed: true };
    }

    /// <summary>
    /// Returns a previously-cached parsed match response, or null if none
    /// exists yet — never triggers a network call.
    /// </summary>
    public async Task<string?> GetCachedParsedMatchAsync(string matchDetailEndpoint, CancellationToken ct)
    {
        var cached = await _cache.ReadAsync(matchDetailEndpoint, ct).ConfigureAwait(false);
        return cached is { IsParsed: true } ? cached.Body : null;
    }

    /// <summary>Caches a freshly-fetched parsed match response, explicitly flagged so it is never treated as an unparsed summary later.</summary>
    public Task CacheParsedMatchAsync(string matchDetailEndpoint, string rawJson, CancellationToken ct) =>
        _cache.WriteAsync(matchDetailEndpoint, rawJson, statusCode: 200, isParsed: true, ct);

    private static OpenDotaResponse ToResponse(RawResponseCache.CachedEntry cached, bool servedFromCache) => new()
    {
        RawJson = cached.Body,
        StatusCode = cached.StatusCode,
        FetchedAtUtc = cached.FetchedAtUtc,
        ServedFromCache = servedFromCache,
    };
}
