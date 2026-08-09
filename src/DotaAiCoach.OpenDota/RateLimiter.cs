namespace DotaAiCoach.OpenDota;

/// <summary>
/// Sliding-window rate limiter over TimeProvider (so tests use a fake
/// clock), plus a daily counter persisted to disk so the budget survives
/// process restarts. Also reacts to the API's own reported remaining-
/// minute header, pausing early if the true remaining budget is lower than
/// what this process alone has tracked (e.g. another process is sharing
/// the same API key).
/// </summary>
public sealed class RateLimiter
{
    private readonly OpenDotaOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<DateTimeOffset> _recentRequests = new();
    private readonly string? _dailyCounterPath;
    private readonly object _lock = new();

    private int _reportedRemainingMinute = int.MaxValue;

    public RateLimiter(OpenDotaOptions options, TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dailyCounterPath = string.IsNullOrEmpty(options.ResolvedCacheDirectory)
            ? null
            : Path.Combine(options.ResolvedCacheDirectory, "ratelimit.json");
    }

    public void ReportRemaining(int? remainingMinute)
    {
        if (remainingMinute is int value)
        {
            lock (_lock)
            {
                _reportedRemainingMinute = value;
            }
        }
    }

    public async Task WaitForSlotAsync(CancellationToken ct)
    {
        while (true)
        {
            TimeSpan? waitFor;
            lock (_lock)
            {
                var now = _timeProvider.GetUtcNow();
                while (_recentRequests.Count > 0 && now - _recentRequests.Peek() >= TimeSpan.FromMinutes(1))
                {
                    _recentRequests.Dequeue();
                }

                var localBudgetExceeded = _recentRequests.Count >= _options.RequestsPerMinute;
                var reportedBudgetExceeded = _reportedRemainingMinute <= 1;

                if (!localBudgetExceeded && !reportedBudgetExceeded)
                {
                    _recentRequests.Enqueue(now);
                    waitFor = null;
                }
                else
                {
                    var oldest = _recentRequests.Count > 0 ? _recentRequests.Peek() : now;
                    waitFor = TimeSpan.FromMinutes(1) - (now - oldest);
                    if (waitFor < TimeSpan.Zero)
                    {
                        waitFor = TimeSpan.FromSeconds(1);
                    }
                }
            }

            if (waitFor is null)
            {
                return;
            }

            await Task.Delay(waitFor.Value, ct).ConfigureAwait(false);
        }
    }

    public async Task<bool> TryConsumeDailyBudgetAsync(CancellationToken ct)
    {
        if (_dailyCounterPath is null)
        {
            return true;
        }

        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var (date, count) = await ReadDailyCounterAsync(ct).ConfigureAwait(false);

        if (date != today)
        {
            count = 0;
        }

        if (count >= _options.RequestsPerDay)
        {
            return false;
        }

        await WriteDailyCounterAsync(today, count + 1, ct).ConfigureAwait(false);
        return true;
    }

    private async Task<(DateTime Date, int Count)> ReadDailyCounterAsync(CancellationToken ct)
    {
        if (_dailyCounterPath is null || !File.Exists(_dailyCounterPath))
        {
            return (default, 0);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_dailyCounterPath, ct).ConfigureAwait(false);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<DailyCounterState>(json);
            return parsed is null ? (default, 0) : (parsed.Date, parsed.Count);
        }
        catch (IOException)
        {
            return (default, 0);
        }
        catch (System.Text.Json.JsonException)
        {
            return (default, 0);
        }
    }

    private async Task WriteDailyCounterAsync(DateTime date, int count, CancellationToken ct)
    {
        if (_dailyCounterPath is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_dailyCounterPath)!);
        var json = System.Text.Json.JsonSerializer.Serialize(new DailyCounterState(date, count));
        var tempPath = _dailyCounterPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
        File.Move(tempPath, _dailyCounterPath, overwrite: true);
    }

    private sealed record DailyCounterState(DateTime Date, int Count);
}
