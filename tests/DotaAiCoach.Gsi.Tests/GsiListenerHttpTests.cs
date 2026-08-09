using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Listener;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using DotaAiCoach.Gsi.Replay;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotaAiCoach.Gsi.Tests;

/// <summary>
/// The one real-HTTP integration test per the project's replay-simulator
/// design: proves the Kestrel listener itself (binding, request handling)
/// works end to end, not just the in-process pipeline that every other
/// test exercises.
/// </summary>
public class GsiListenerHttpTests
{
    private static string FixtureDir(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", name);

    [Fact]
    public async Task Replaying_fixtures_over_http_matches_in_process_result_and_returns_200()
    {
        var options = new GsiOptions { Port = 0, Route = "/gsi" };
        var pipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(options));
        var listener = new GsiListener(options, pipeline, NullLogger<GsiListener>.Instance);

        await listener.StartAsync(CancellationToken.None);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{listener.BoundPort}/") };
            var target = new HttpReplayTarget(client, "gsi", pipeline);
            var simulator = new GsiReplaySimulator(target);

            var fixtures = FixtureSequenceLoader.Load(FixtureDir("laning-sequence"));
            var steps = await simulator.RunToCompletionAsync(fixtures);

            steps.Should().HaveCount(6);
            steps[^1].StateAfter.Player.Gold.Should().Be(112);
            steps[^1].StateAfter.Items.Inventory[4].Name.Should().Be("item_recipe_magic_wand");
        }
        finally
        {
            await listener.StopAsync(CancellationToken.None);
            await listener.DisposeAsync();
        }
    }

    [Fact]
    public async Task Payload_with_wrong_auth_token_is_dropped_not_merged()
    {
        var options = new GsiOptions { Port = 0, Route = "/gsi", AuthToken = "expected-token" };
        var pipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(options));
        var listener = new GsiListener(options, pipeline, NullLogger<GsiListener>.Instance);

        await listener.StartAsync(CancellationToken.None);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{listener.BoundPort}/") };
            var payloadWithWrongToken = """{"auth":{"token":"wrong"},"player":{"gold":9999}}""";

            var response = await client.PostAsync("gsi", new StringContent(payloadWithWrongToken));
            response.EnsureSuccessStatusCode();

            await Task.Delay(50);
            pipeline.State.Player.Gold.Should().BeNull("a payload with an incorrect auth token must be dropped, not merged");
        }
        finally
        {
            await listener.StopAsync(CancellationToken.None);
            await listener.DisposeAsync();
        }
    }
}
