using DotaAiCoach.OpenDota;

namespace DotaAiCoach.Host;

/// <summary>
/// The only construction entry point consumers (the overlay) should use
/// for player-profile data. Keeps OpenDota HTTP/cache/rate-limiter
/// construction details inside this assembly so UiOverlay never needs a
/// reference to DotaAiCoach.OpenDota — see docs/adr/0012.
/// </summary>
public static class ProfileSessionFactory
{
    public static IProfileSession Create(string? cacheDirectory = null, string? apiKey = null)
    {
        var options = new OpenDotaOptions { CacheDirectory = cacheDirectory, ApiKey = apiKey };
        var cache = new RawResponseCache(options.ResolvedCacheDirectory);
        var rateLimiter = new RateLimiter(options);
        var httpClient = new OpenDotaHttpClient(options, rateLimiter);
        var cachingClient = new CachingOpenDotaClient(httpClient, cache, offlineOnly: options.OfflineOnly);
        var service = new PlayerProfileService(cachingClient, options);

        // httpClient is also passed as the raw IOpenDotaClient for the
        // Deep DNA Refresh's parse-request flow (POST /request, poll GET
        // /request/{jobId}) — these calls are deliberately never routed
        // through the caching decorator, since a parse trigger only makes
        // sense as a live call (see CachingOpenDotaClient.PostAsync).
        // cachingClient is passed separately for GetCurrentGamePatchAsync,
        // which — unlike the parse flow — benefits from disk caching
        // (the current patch changes rarely, no reason to hit the network
        // every session).
        return new ProfileSession(service, httpClient, cachingClient, options);
    }
}
