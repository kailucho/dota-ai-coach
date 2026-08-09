namespace DotaAiCoach.FairPlay;

/// <summary>
/// Where a classified piece of data physically originates. Used alongside
/// <see cref="FairPlayClassification"/> so the "source" field of the
/// project's required five-field documentation is machine-checkable, not
/// just prose.
/// </summary>
public enum DataSourceKind
{
    /// <summary>Dota 2 Game State Integration, sourced from the player's own feed.</summary>
    Gsi,

    /// <summary>Computer vision reading the player's own rendered screen.</summary>
    ComputerVision,

    /// <summary>Static, patch-versioned reference data (hero/item mechanics).</summary>
    StaticReference,

    /// <summary>An external API (e.g. OpenDota, STRATZ).</summary>
    ExternalApi,

    /// <summary>Full match replay data, only available post-game.</summary>
    Replay
}
