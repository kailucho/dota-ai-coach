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

        return new ProfileSession(service, options);
    }
}
