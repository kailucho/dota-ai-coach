using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.Host;

/// <summary>
/// The only surface DotaAiCoach.UiOverlay is allowed to depend on for
/// player profile data. Exposes exclusively DotaAiCoach.PlayerProfile
/// types — no OpenDota HTTP/wire details — mirroring ICoachSession's
/// firewall for GSI. See docs/adr/0012.
/// </summary>
public interface IProfileSession
{
    PlayerProfileSnapshot Current { get; }
    bool IsRefreshing { get; }
    bool IsDeepRefreshing { get; }

    event EventHandler<PlayerProfileSnapshot>? Updated;

    Task<PlayerProfileSnapshot> RefreshAsync(string steamIdInput, CancellationToken ct);
    Task<PlayerProfileSnapshot> LoadFromCacheAsync(string steamIdInput, CancellationToken ct);

    /// <summary>
    /// Parses up to maxMatches of the player's most recent ranked matches
    /// to unlock the parsed-data Dota DNA dimensions (see docs/adr/0016).
    /// Expensive and explicit — never called automatically. Requires
    /// Current to already be an Available snapshot (call RefreshAsync
    /// first).
    /// </summary>
    Task<PlayerProfileSnapshot> RequestDeepDnaRefreshAsync(string steamIdInput, int maxMatches, IProgress<DeepDnaRefreshProgress>? progress, CancellationToken ct);
}

public sealed record DeepDnaRefreshProgress(int Completed, int Total);
