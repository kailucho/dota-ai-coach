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

    private static OpenDotaResponse ToResponse(RawResponseCache.CachedEntry cached, bool servedFromCache) => new()
    {
        RawJson = cached.Body,
        StatusCode = cached.StatusCode,
        FetchedAtUtc = cached.FetchedAtUtc,
        ServedFromCache = servedFromCache,
    };
}
