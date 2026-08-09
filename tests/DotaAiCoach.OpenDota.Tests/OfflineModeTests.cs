using DotaAiCoach.OpenDota;
using DotaAiCoach.PlayerProfile.Model;
using FluentAssertions;

namespace DotaAiCoach.OpenDota.Tests;

public class OfflineModeTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dota-ai-coach-offline-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class ThrowingClientIfCalled : IOpenDotaClient
    {
        public Task<OpenDotaResponse> GetAsync(string relativePath, CancellationToken ct) =>
            throw new InvalidOperationException("OfflineOnly must never call the inner client.");
    }

    [Fact]
    public async Task OfflineOnly_option_never_constructs_an_http_request_and_returns_cached_data()
    {
        var dir = NewTempDir();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new RawResponseCache(dir, clock);
        await cache.WriteAsync("/players/1", "offline-cached-body", 200, CancellationToken.None);

        var client = new CachingOpenDotaClient(new ThrowingClientIfCalled(), cache, offlineOnly: true, clock);

        var response = await client.GetAsync("/players/1", CancellationToken.None);

        response.RawJson.Should().Be("offline-cached-body");
    }

    [Fact]
    public async Task Cold_cache_in_offline_mode_returns_Unknown_availability_not_an_exception()
    {
        var dir = NewTempDir();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new RawResponseCache(dir, clock);

        var client = new CachingOpenDotaClient(new ThrowingClientIfCalled(), cache, offlineOnly: true, clock);

        var act = async () => await client.GetWithAvailabilityAsync("/players/1", TimeSpan.Zero, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var (response, availability) = await client.GetWithAvailabilityAsync("/players/1", TimeSpan.Zero, CancellationToken.None);
        response.RawJson.Should().BeNull();
        availability.Should().Be(ProfileAvailability.Unknown);
    }
}
