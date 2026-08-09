using System.Runtime.CompilerServices;
using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;

namespace DotaAiCoach.Gsi.Replay;

public enum ReplaySpeed
{
    /// <summary>No delay between steps — fast, deterministic, used by automated tests.</summary>
    Instant,

    /// <summary>Delay between steps derived from each fixture's provider.timestamp delta, for manual debugging against real match pacing.</summary>
    RealTime,

    /// <summary>Fixed delay between every step, regardless of fixture timestamps.</summary>
    Fixed
}

public sealed record ReplayOptions
{
    public ReplaySpeed Speed { get; init; } = ReplaySpeed.Instant;
    public TimeSpan FixedInterval { get; init; } = TimeSpan.FromSeconds(1);
    public double SpeedMultiplier { get; init; } = 1.0;
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UnixEpoch;
}

public sealed record ReplayStep(
    FixturePayload Payload,
    MatchState StateAfter,
    IReadOnlyList<DomainEvent> Events,
    FeedHealth Health,
    DateTimeOffset SimulatedTime);

/// <summary>
/// The destination a replay sequence is fed into. Two implementations:
/// <see cref="InProcessReplayTarget"/> (default — deterministic, no port
/// contention, ideal for assert-after-each-step tests) and
/// <see cref="HttpReplayTarget"/> (drives a real listener over HTTP, for
/// the one true end-to-end integration test and for manual debugging
/// against a running overlay).
/// </summary>
public interface IReplayTarget
{
    Task<ReplayStep> SendAsync(FixturePayload payload, DateTimeOffset simulatedTime, CancellationToken ct);
}

/// <summary>
/// Feeds a fixture sequence through an <see cref="IReplayTarget"/> at a
/// controllable pace, without needing a live Dota client. Simulated time
/// advances by each fixture's own provider.timestamp delta in
/// <see cref="ReplaySpeed.RealTime"/> mode rather than by wall-clock time,
/// so staleness/discontinuity logic can be exercised deterministically.
/// The simulator itself parses nothing beyond provider.timestamp — it
/// stays free of GSI-specific field knowledge so it works identically for
/// PlayingGameState fixtures today and any future fixture shape.
/// </summary>
public sealed class GsiReplaySimulator
{
    private readonly IReplayTarget _target;
    private readonly ReplayOptions _options;

    public GsiReplaySimulator(IReplayTarget target, ReplayOptions? options = null)
    {
        _target = target;
        _options = options ?? new ReplayOptions();
    }

    public async IAsyncEnumerable<ReplayStep> RunAsync(
        IReadOnlyList<FixturePayload> sequence, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var simulatedTime = _options.StartTime;
        long? previousTimestamp = null;

        foreach (var payload in sequence)
        {
            ct.ThrowIfCancellationRequested();

            if (_options.Speed == ReplaySpeed.RealTime && payload.ProviderTimestamp is { } ts)
            {
                if (previousTimestamp is { } prev && ts > prev)
                {
                    var deltaSeconds = (ts - prev) / Math.Max(_options.SpeedMultiplier, 0.0001);
                    await Task.Delay(TimeSpan.FromSeconds(deltaSeconds), ct);
                }
                previousTimestamp = ts;
            }
            else if (_options.Speed == ReplaySpeed.Fixed)
            {
                await Task.Delay(_options.FixedInterval, ct);
            }

            if (payload.ProviderTimestamp is { } tsForClock)
            {
                simulatedTime = DateTimeOffset.FromUnixTimeSeconds(tsForClock);
            }
            else
            {
                simulatedTime = simulatedTime.AddSeconds(1);
            }

            yield return await _target.SendAsync(payload, simulatedTime, ct);
        }
    }

    public async Task<IReadOnlyList<ReplayStep>> RunToCompletionAsync(
        IReadOnlyList<FixturePayload> sequence, CancellationToken ct = default)
    {
        var steps = new List<ReplayStep>();
        await foreach (var step in RunAsync(sequence, ct))
        {
            steps.Add(step);
        }
        return steps;
    }
}

/// <summary>Feeds fixtures directly into a <see cref="GsiPipeline"/>, in-process. Deterministic, no network involved.</summary>
public sealed class InProcessReplayTarget : IReplayTarget
{
    private readonly GsiPipeline _pipeline;

    public InProcessReplayTarget(GsiPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<ReplayStep> SendAsync(FixturePayload payload, DateTimeOffset simulatedTime, CancellationToken ct)
    {
        var result = _pipeline.Ingest(payload.RawJson, simulatedTime);
        var events = result?.Events ?? Array.Empty<DomainEvent>();
        var health = result?.Health ?? _pipeline.EvaluateHealth(simulatedTime);

        // Snapshot via Clone(): pipeline.State is a mutable, shared instance
        // that later steps continue to mutate in place, so callers inspecting
        // a specific historical step must not hold a live reference to it.
        return Task.FromResult(new ReplayStep(payload, _pipeline.State.Clone(), events, health, simulatedTime));
    }
}
