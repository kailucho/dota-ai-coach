namespace DotaAiCoach.OpenDota;

public sealed class OpenDotaOptions
{
    public string BaseUrl { get; set; } = "https://api.opendota.com/api";
    public string? ApiKey { get; set; }
    public int RequestsPerMinute { get; set; } = 55;
    public int RequestsPerDay { get; set; } = 2800;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public string? CacheDirectory { get; set; }
    public TimeSpan ProfileTtl { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan MatchesTtl { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan HeroesTtl { get; set; } = TimeSpan.FromDays(7);
    public int RecentMatchLimit { get; set; } = 100;

    /// <summary>When true, never issue a network request; only cached data is used, per the offline-first requirement.</summary>
    public bool OfflineOnly { get; set; }

    public string ResolvedCacheDirectory =>
        CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach", "cache", "opendota");
}
