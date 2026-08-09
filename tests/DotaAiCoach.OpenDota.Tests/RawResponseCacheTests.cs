using DotaAiCoach.OpenDota;
using FluentAssertions;

namespace DotaAiCoach.OpenDota.Tests;

public class RawResponseCacheTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dota-ai-coach-cache-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Raw_body_is_stored_verbatim_so_no_field_is_lost_before_normalization()
    {
        var dir = NewTempDir();
        var cache = new RawResponseCache(dir);
        var body = """{"unexpected_new_field":"value","account_id":1}""";

        await cache.WriteAsync("/players/1", body, 200, CancellationToken.None);
        var entry = await cache.ReadAsync("/players/1", CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.Body.Should().Be(body);
    }

    [Fact]
    public async Task Cache_key_is_derived_from_the_endpoint_path_and_query_so_different_limits_do_not_collide()
    {
        var dir = NewTempDir();
        var cache = new RawResponseCache(dir);

        await cache.WriteAsync("/players/1/matches?limit=10", "a", 200, CancellationToken.None);
        await cache.WriteAsync("/players/1/matches?limit=100", "b", 200, CancellationToken.None);

        var ten = await cache.ReadAsync("/players/1/matches?limit=10", CancellationToken.None);
        var hundred = await cache.ReadAsync("/players/1/matches?limit=100", CancellationToken.None);

        ten!.Body.Should().Be("a");
        hundred!.Body.Should().Be("b");
    }

    [Fact]
    public async Task A_response_within_ttl_is_served_from_cache_without_a_network_call()
    {
        var dir = NewTempDir();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new RawResponseCache(dir, clock);
        var client = new CachingOpenDotaClient(inner: null, cache, offlineOnly: false, clock);

        await cache.WriteAsync("/players/1", "cached-body", 200, CancellationToken.None);

        var response = await client.GetAsync("/players/1", CancellationToken.None);

        response.RawJson.Should().Be("cached-body");
        response.ServedFromCache.Should().BeTrue();
    }

    private sealed class FailingClient : IOpenDotaClient
    {
        public Task<OpenDotaResponse> GetAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult(new OpenDotaResponse { RawJson = null, StatusCode = null, FetchedAtUtc = DateTimeOffset.UtcNow });

        public Task<OpenDotaResponse> PostAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult(new OpenDotaResponse { RawJson = null, StatusCode = null, FetchedAtUtc = DateTimeOffset.UtcNow });
    }

    [Fact]
    public async Task A_stale_entry_is_served_when_the_network_fails_so_the_app_works_offline()
    {
        var dir = NewTempDir();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new RawResponseCache(dir, clock);
        await cache.WriteAsync("/players/1", "stale-body", 200, CancellationToken.None);

        var client = new CachingOpenDotaClient(new FailingClient(), cache, offlineOnly: false, clock);
        var (response, staleAvailability) = await client.GetWithAvailabilityAsync("/players/1", TimeSpan.Zero, CancellationToken.None);

        response.RawJson.Should().Be("stale-body");
        staleAvailability.Should().NotBeNull();
    }

    [Fact]
    public async Task Serving_a_stale_entry_sets_CacheOnlyStale_so_the_ui_can_say_the_data_is_old()
    {
        var dir = NewTempDir();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new RawResponseCache(dir, clock);
        await cache.WriteAsync("/players/1", "stale-body", 200, CancellationToken.None);

        var client = new CachingOpenDotaClient(new FailingClient(), cache, offlineOnly: false, clock);
        var (_, staleAvailability) = await client.GetWithAvailabilityAsync("/players/1", TimeSpan.Zero, CancellationToken.None);

        staleAvailability.Should().Be(PlayerProfile.Model.ProfileAvailability.CacheOnlyStale);
    }

    [Fact]
    public async Task Write_is_atomic_so_a_crash_mid_write_cannot_leave_a_truncated_cache_file()
    {
        var dir = NewTempDir();
        var cache = new RawResponseCache(dir);

        await cache.WriteAsync("/players/1", "final-content", 200, CancellationToken.None);

        Directory.GetFiles(dir, "*.tmp").Should().BeEmpty("temp files must be renamed away, never left behind");
        var entry = await cache.ReadAsync("/players/1", CancellationToken.None);
        entry!.Body.Should().Be("final-content");
    }
}
