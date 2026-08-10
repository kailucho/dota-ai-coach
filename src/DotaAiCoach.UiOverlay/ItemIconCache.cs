using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// Resolves an item icon image from Steam's public CDN
/// (cdn.cloudflare.steamstatic.com/apps/dota2/images/items/{name}_lg.png,
/// same URL scheme OpenDota/Dotabuff use), caching each icon to disk
/// under %LocalAppData%\DotaAiCoach\item-icons after the first download
/// so a live match never re-downloads the same icon twice and the
/// overlay stays usable (from cache) if Steam's CDN is briefly
/// unreachable. Purely a presentation concern — RecommendationEngines/
/// Domain never know icons exist, per dota-architecture's UI/domain
/// separation.
/// </summary>
public sealed class ItemIconCache
{
    private const string BaseUrl = "https://cdn.cloudflare.steamstatic.com/apps/dota2/images/items/";
    private static readonly HttpClient Http = new();

    private readonly string _cacheDirectory;
    private readonly Dictionary<string, BitmapImage?> _memoryCache = new();

    public ItemIconCache(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach", "item-icons");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Returns the cached/downloaded icon for a GSI-style item id (e.g.
    /// "item_tango"), or null if it could not be obtained (network
    /// failure, unknown item) — callers must degrade to text-only display
    /// rather than treat a null as an error.
    /// </summary>
    public async Task<BitmapImage?> GetIconAsync(string itemId, CancellationToken ct)
    {
        if (_memoryCache.TryGetValue(itemId, out var cached))
        {
            return cached;
        }

        var steamName = StripItemPrefix(itemId);
        var filePath = Path.Combine(_cacheDirectory, $"{steamName}.png");

        try
        {
            if (!File.Exists(filePath))
            {
                var bytes = await Http.GetByteArrayAsync($"{BaseUrl}{steamName}_lg.png", ct);
                await File.WriteAllBytesAsync(filePath, bytes, ct);
            }

            var image = LoadBitmap(filePath);
            _memoryCache[itemId] = image;
            return image;
        }
        catch
        {
            // Network failure, 404 for an unmapped item id, or a corrupt
            // cached file — degrade to "no icon" rather than throw; the
            // overlay must keep working with text-only display.
            _memoryCache[itemId] = null;
            return null;
        }
    }

    private static BitmapImage LoadBitmap(string filePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(filePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string StripItemPrefix(string itemId) =>
        itemId.StartsWith("item_", StringComparison.Ordinal) ? itemId["item_".Length..] : itemId;
}
