namespace DotaAiCoach.Gsi;

/// <summary>Configuration for the GSI listener/normalizer pipeline.</summary>
public sealed class GsiOptions
{
    /// <summary>Loopback-only bind port. 3000 is the conventional default used by community GSI examples.</summary>
    public int Port { get; set; } = 3000;

    public string Route { get; set; } = "/gsi";

    /// <summary>Optional shared token expected in payload.auth.token. A mismatch causes the payload to be dropped, not 401'd.</summary>
    public string? AuthToken { get; set; }

    /// <summary>How long without a payload before the feed is considered stale. Should exceed the .cfg heartbeat interval with tolerance.</summary>
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Gap between payloads large enough to flag a reconnection/discontinuity, distinct from ordinary staleness.</summary>
    public TimeSpan DiscontinuityThreshold { get; set; } = TimeSpan.FromSeconds(60);
}
