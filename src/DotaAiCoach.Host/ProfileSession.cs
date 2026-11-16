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

        // Valve's own datafeed lists patches including the sub-patch letter
        // (e.g. "7.41e"), which matters because item numbers get tuned in
        // lettered patches. OpenDota's /constants/patch only carries the
        // major version ("7.41"), so we prefer the datafeed and fall back
        // to OpenDota if it is unreachable.
        _cachedGamePatch = await TryGetPatchFromDatafeedAsync(ct).ConfigureAwait(false)
            ?? await TryGetPatchFromOpenDotaAsync(ct).ConfigureAwait(false);

        return _cachedGamePatch;
    }

    private async Task<string?> TryGetPatchFromDatafeedAsync(CancellationToken ct)
    {
        var (response, _) = await _cachingClient
            .GetWithAvailabilityAsync("https://www.dota2.com/datafeed/patchnoteslist", GamePatchTtl, ct)
            .ConfigureAwait(false);
        if (response.RawJson is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(response.RawJson);
            if (!doc.RootElement.TryGetProperty("patches", out var patches) || patches.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            // Newest patch = highest timestamp; the array order is not guaranteed.
            long newestTs = long.MinValue;
            string? newestName = null;
            foreach (var patch in patches.EnumerateArray())
            {
                if (patch.TryGetProperty("patch_timestamp", out var tsProp) &&
                    tsProp.TryGetInt64(out var ts) && ts > newestTs &&
                    patch.TryGetProperty("patch_name", out var nameProp))
                {
                    newestTs = ts;
                    newestName = nameProp.GetString();
                }
            }

            return string.IsNullOrWhiteSpace(newestName) ? null : newestName;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> TryGetPatchFromOpenDotaAsync(CancellationToken ct)
    {
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
                return nameProp.GetString();
            }
        }
        catch (JsonException)
        {
            // Malformed/unexpected response shape — return null rather than
            // guessing; the overlay shows nothing instead of a wrong patch.
        }

        return null;
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
