namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>
/// Provenance metadata attached to every fetched profile snapshot, per
/// dota-data rule 5 (track source, fetch time, pipeline stage/version).
/// </summary>
public sealed record ProfileProvenance
{
    public DateTimeOffset? FetchedAtUtc { get; init; }
    public string? SourceName { get; init; }
    public bool ServedFromCache { get; init; }

    public static readonly ProfileProvenance Empty = new();
}
