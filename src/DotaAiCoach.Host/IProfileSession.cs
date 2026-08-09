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

    event EventHandler<PlayerProfileSnapshot>? Updated;

    Task<PlayerProfileSnapshot> RefreshAsync(string steamIdInput, CancellationToken ct);
    Task<PlayerProfileSnapshot> LoadFromCacheAsync(string steamIdInput, CancellationToken ct);
}
