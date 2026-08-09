using DotaAiCoach.OpenDota;
using FluentAssertions;

namespace DotaAiCoach.OpenDota.Tests;

public class RateLimiterTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dota-ai-coach-ratelimit-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Requests_spread_across_two_minutes_never_wait()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = new OpenDotaOptions { RequestsPerMinute = 5, CacheDirectory = NewTempDir() };
        var limiter = new RateLimiter(options, clock);

        for (var i = 0; i < 5; i++)
        {
            await limiter.WaitForSlotAsync(CancellationToken.None);
        }

        clock.Advance(TimeSpan.FromMinutes(1.1));

        var task = limiter.WaitForSlotAsync(CancellationToken.None);
        await Task.Delay(50);

        task.IsCompleted.Should().BeTrue("the sliding window should have rolled past the first batch by now");
    }

    [Fact]
    public async Task A_reported_remaining_minute_of_zero_pauses_until_the_window_resets_even_below_the_local_count()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = new OpenDotaOptions { RequestsPerMinute = 55, CacheDirectory = NewTempDir() };
        var limiter = new RateLimiter(options, clock);

        limiter.ReportRemaining(0);

        var task = limiter.WaitForSlotAsync(CancellationToken.None);
        await Task.Delay(50);

        task.IsCompleted.Should().BeFalse("a reported remaining-minute of 0 should block regardless of the local counter");
    }

    [Fact]
    public async Task Daily_counter_persists_across_instances_so_a_restart_cannot_reset_the_budget()
    {
        var dir = NewTempDir();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = new OpenDotaOptions { RequestsPerDay = 2, CacheDirectory = dir };

        var first = new RateLimiter(options, clock);
        (await first.TryConsumeDailyBudgetAsync(CancellationToken.None)).Should().BeTrue();
        (await first.TryConsumeDailyBudgetAsync(CancellationToken.None)).Should().BeTrue();

        var second = new RateLimiter(options, clock);
        (await second.TryConsumeDailyBudgetAsync(CancellationToken.None)).Should().BeFalse(
            "the daily count of 2 was already exhausted by the first instance and must persist across a restart");
    }
}
