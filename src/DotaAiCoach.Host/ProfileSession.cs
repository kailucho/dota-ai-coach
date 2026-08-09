using DotaAiCoach.OpenDota;
using DotaAiCoach.PlayerProfile.Identity;
using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.Host;

internal sealed class ProfileSession : IProfileSession
{
    private readonly PlayerProfileService _service;
    private readonly OpenDotaOptions _options;

    public PlayerProfileSnapshot Current { get; private set; } = PlayerProfileSnapshot.Empty;
    public bool IsRefreshing { get; private set; }

    public event EventHandler<PlayerProfileSnapshot>? Updated;

    public ProfileSession(PlayerProfileService service, OpenDotaOptions options)
    {
        _service = service;
        _options = options;
    }

    public async Task<PlayerProfileSnapshot> RefreshAsync(string steamIdInput, CancellationToken ct)
    {
        if (!SteamId.TryParse(steamIdInput, out var id))
        {
            return Current;
        }

        IsRefreshing = true;
        try
        {
            var snapshot = await _service.FetchAsync(id, ct).ConfigureAwait(false);
            Current = snapshot;
            Updated?.Invoke(this, snapshot);
            return snapshot;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task<PlayerProfileSnapshot> LoadFromCacheAsync(string steamIdInput, CancellationToken ct)
    {
        if (!SteamId.TryParse(steamIdInput, out var id))
        {
            return Current;
        }

        var offlineService = new PlayerProfileService(
            new CachingOpenDotaClient(inner: null, new RawResponseCache(_options.ResolvedCacheDirectory), offlineOnly: true),
            _options);

        var snapshot = await offlineService.FetchAsync(id, ct).ConfigureAwait(false);
        Current = snapshot;
        Updated?.Invoke(this, snapshot);
        return snapshot;
    }
}
