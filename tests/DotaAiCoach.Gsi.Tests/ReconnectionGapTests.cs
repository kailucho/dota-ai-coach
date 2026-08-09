using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using DotaAiCoach.Gsi.Replay;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class ReconnectionGapTests
{
    private static string FixtureDir(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", name);

    [Fact]
    public async Task Large_gap_between_payloads_fires_discontinuity_and_does_not_reset_state()
    {
        var options = new GsiOptions { DiscontinuityThreshold = TimeSpan.FromSeconds(60) };
        var pipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(options));
        var target = new InProcessReplayTarget(pipeline);
        // RealTime speed makes the simulator advance simulated time by each
        // fixture's own provider.timestamp delta rather than wall-clock time,
        // so a 120s gap can be asserted deterministically without waiting.
        var simulator = new GsiReplaySimulator(target, new ReplayOptions { Speed = ReplaySpeed.RealTime, SpeedMultiplier = 1000 });

        var fixtures = FixtureSequenceLoader.Load(FixtureDir("reconnection-gap"));
        var steps = await simulator.RunToCompletionAsync(fixtures);

        steps[0].Events.OfType<FeedDiscontinuityDetected>().Should().BeEmpty();
        steps[1].Events.OfType<FeedDiscontinuityDetected>().Should().ContainSingle();

        // State survives the gap rather than being reset.
        steps[1].StateAfter.Player.Gold.Should().Be(3100);
        steps[1].StateAfter.HasStateDiscontinuity.Should().BeTrue();
    }

    [Fact]
    public void EvaluateHealth_without_any_payload_reports_never_connected()
    {
        var pipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(new GsiOptions()));

        pipeline.EvaluateHealth(DateTimeOffset.UtcNow).Status.Should().Be(FeedStatus.NeverConnected);
    }

    [Fact]
    public void Normalization_after_a_gap_does_not_throw_and_produces_coherent_state()
    {
        var options = new GsiOptions { DiscontinuityThreshold = TimeSpan.FromSeconds(60) };
        var pipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(options));

        var t0 = DateTimeOffset.UtcNow;
        pipeline.Ingest("""{"player":{"gold":1000}}""", t0);

        var act = () => pipeline.Ingest("""{"player":{"gold":1500}}""", t0.AddMinutes(5));

        act.Should().NotThrow();
        pipeline.State.Player.Gold.Should().Be(1500);
    }
}
