namespace DotaAiCoach.PlayerProfile.Dna;

/// <summary>
/// Why a Dota DNA dimension does or doesn't have a computed score. A
/// deferred dimension must report a truthful reason here rather than a
/// fabricated score — see docs/adr/0014.
/// </summary>
public enum DnaAvailability
{
    /// <summary>A score was computed from available data.</summary>
    Computed,

    /// <summary>Computable in principle, but too few matches carried the required fields.</summary>
    InsufficientData,

    /// <summary>Requires OpenDota's parsed match detail (e.g. purchase_log, obs_placed), not available from match summaries.</summary>
    RequiresParsedMatches,

    /// <summary>Requires raw replay analysis, not available from any API.</summary>
    RequiresReplay,

    /// <summary>Deliberately not built in this phase (see docs/adr/0014).</summary>
    NotYetImplemented,
}
