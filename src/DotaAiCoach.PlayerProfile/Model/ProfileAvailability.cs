namespace DotaAiCoach.PlayerProfile.Model;

/// <summary>
/// The first-class state of a fetched profile, per dota-data rule 6:
/// private/missing profiles must be handled explicitly, never silently
/// rendered as zero games or as an unhandled error.
/// </summary>
public enum ProfileAvailability
{
    /// <summary>No fetch has been attempted yet.</summary>
    Unknown,

    /// <summary>Profile and match history are public and were fetched successfully.</summary>
    Available,

    /// <summary>OpenDota reports fh_unavailable, or an open profile with an
    /// empty match history and a zero win/loss record — the account has
    /// "Expose Public Match Data" turned off in Dota 2.</summary>
    MatchHistoryPrivate,

    /// <summary>OpenDota returned 404 for this account id.</summary>
    PlayerNotFound,

    /// <summary>The fetch failed (network/HTTP error) and no cached data exists.</summary>
    FetchFailed,

    /// <summary>The network fetch failed but a stale cached snapshot was served instead.</summary>
    CacheOnlyStale,
}
