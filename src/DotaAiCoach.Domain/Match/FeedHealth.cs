namespace DotaAiCoach.Domain.Match;

/// <summary>
/// Connection health of the GSI feed. Per dota-gsi's stale-state guidance,
/// staleness must be detected proactively (via a heartbeat-based timeout),
/// not only reactively when a payload happens to arrive — see
/// GsiPipeline.EvaluateHealth in DotaAiCoach.Gsi.
/// </summary>
public enum FeedStatus
{
    NeverConnected,
    Live,
    Stale
}

public sealed record FeedHealth
{
    public FeedStatus Status { get; init; } = FeedStatus.NeverConnected;
    public DateTimeOffset? LastPayloadReceivedAt { get; init; }
    public double? SecondsSinceLastUpdate { get; init; }

    public bool IsStale => Status == FeedStatus.Stale;

    public static readonly FeedHealth NeverConnectedDefault = new();
}
