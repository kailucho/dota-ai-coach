namespace DotaAiCoach.OpenDota;

/// <summary>
/// Returns raw JSON, not deserialized DTOs — this is what makes the RAW
/// cache and fixture-based mapper tests trivial: the cache stores exactly
/// what the network returned, and tests can feed captured JSON directly to
/// OpenDotaProfileMapper without needing a live HTTP call.
/// </summary>
public interface IOpenDotaClient
{
    Task<OpenDotaResponse> GetAsync(string relativePath, CancellationToken ct);
}

public sealed record OpenDotaResponse
{
    public string? RawJson { get; init; }
    public int? StatusCode { get; init; }
    public DateTimeOffset FetchedAtUtc { get; init; }
    public bool ServedFromCache { get; init; }
    public int? RateLimitRemainingMinute { get; init; }
    public int? RateLimitRemainingDay { get; init; }
}
