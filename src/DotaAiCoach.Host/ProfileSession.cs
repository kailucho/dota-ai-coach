using System.Text.Json;
using DotaAiCoach.OpenDota;
using DotaAiCoach.PlayerProfile.Identity;
using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.Host;

internal sealed class ProfileSession : IProfileSession
{
    private static readonly TimeSpan GamePatchTtl = TimeSpan.FromDays(1);

    private readonly PlayerProfileService _service;
    private readonly IOpenDotaClient _rawClient;
    private readonly CachingOpenDotaClient _cachingClient;
    private readonly OpenDotaOptions _options;
    private string? _cachedGamePatch;

    public PlayerProfileSnapshot Current { get; private set; } = PlayerProfileSnapshot.Empty;
    public bool IsRefreshing { get; private set; }
    public bool IsDeepRefreshing { get; private set; }

    public event EventHandler<PlayerProfileSnapshot>? Updated;

    public ProfileSession(PlayerProfileService service, IOpenDotaClient rawClient, CachingOpenDotaClient cachingClient, OpenDotaOptions options)
    {
        _service = service;
        _rawClient = rawClient;
        _cachingClient = cachingClient;
        _options = options;
    }

    public async Task<string?> GetCurrentGamePatchAsync(CancellationToken ct)
    {
        if (_cachedGamePatch is not null)
        {
            return _cachedGamePatch;
        }

        var (response, _) = await _cachingClient.GetWithAvailabilityAsync("/constants/patch", GamePatchTtl, ct).ConfigureAwait(false);
        if (response.RawJson is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(response.RawJson);
            var last = doc.RootElement.EnumerateArray().LastOrDefault();
            if (last.ValueKind == JsonValueKind.Object && last.TryGetProperty("name", out var nameProp))
            {
                _cachedGamePatch = nameProp.GetString();
            }
        }
        catch (JsonException)
        {
            // Malformed/unexpected response shape — leave _cachedGamePatch
            // null rather than guessing; the overlay shows nothing instead
            // of a wrong patch.
        }

        return _cachedGamePatch;
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

    public async Task<PlayerProfileSnapshot> RequestDeepDnaRefreshAsync(
        string steamIdInput, int maxMatches, IProgress<DeepDnaRefreshProgress>? progress, CancellationToken ct)
    {
        if (!SteamId.TryParse(steamIdInput, out var id))
        {
            return Current;
        }

        IsDeepRefreshing = true;
        try
        {
            var innerProgress = progress is null
                ? null
                : new Progress<ParseProgress>(p => progress.Report(new DeepDnaRefreshProgress(p.Completed, p.Total)));

            var snapshot = await _service.RequestDeepDnaRefreshAsync(id, Current, _rawClient, maxMatches, innerProgress, ct).ConfigureAwait(false);
            Current = snapshot;
            Updated?.Invoke(this, snapshot);
            return snapshot;
        }
        finally
        {
            IsDeepRefreshing = false;
        }
    }
}
