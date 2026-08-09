using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using DotaAiCoach.Gsi.Replay;

var parsed = CliArgs.Parse(args);
if (parsed is null)
{
    CliArgs.PrintUsage();
    return 1;
}

var fixtures = FixtureSequenceLoader.Load(parsed.FixturesDirectory);
Console.WriteLine($"Loaded {fixtures.Count} fixture(s) from {parsed.FixturesDirectory}");

IReplayTarget target;
GsiPipeline? observedPipeline = null;

if (parsed.HttpEndpoint is not null)
{
    var endpointUri = new Uri(parsed.HttpEndpoint);
    var baseAddress = new Uri(endpointUri.GetLeftPart(UriPartial.Authority) + "/");
    var relativePath = endpointUri.PathAndQuery.TrimStart('/');

    observedPipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(new GsiOptions()));
    using var client = new HttpClient { BaseAddress = baseAddress };
    target = new HttpReplayTarget(client, relativePath, observedPipeline);
    await RunAsync(target);
}
else
{
    observedPipeline = new GsiPipeline(new GsiPayloadParser(), new GsiNormalizer(new GsiOptions()));
    target = new InProcessReplayTarget(observedPipeline);
    await RunAsync(target);
}

return 0;

async Task RunAsync(IReplayTarget replayTarget)
{
    var options = new ReplayOptions
    {
        Speed = parsed.Speed,
        FixedInterval = TimeSpan.FromMilliseconds(parsed.IntervalMs)
    };
    var simulator = new GsiReplaySimulator(replayTarget, options);

    await foreach (var step in simulator.RunAsync(fixtures))
    {
        Console.WriteLine($"[{step.Payload.FileName}] clock={step.StateAfter.Match.ClockTime} " +
                           $"gold={step.StateAfter.Player.Gold} events={step.Events.Count} health={step.Health.Status}");

        if (parsed.PrintState)
        {
            Console.WriteLine($"    hero={step.StateAfter.Hero.Name} level={step.StateAfter.Hero.Level} " +
                               $"alive={step.StateAfter.Hero.Alive} items={string.Join(",", step.StateAfter.Items.Inventory.Where(i => !i.IsEmpty).Select(i => i.Name))}");
        }
    }
}

internal sealed record CliArgs(
    string FixturesDirectory,
    ReplaySpeed Speed,
    int IntervalMs,
    string? HttpEndpoint,
    bool PrintState)
{
    public static CliArgs? Parse(string[] args)
    {
        string? fixturesDir = null;
        var speed = ReplaySpeed.Instant;
        var intervalMs = 1000;
        string? http = null;
        var printState = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--fixtures" when i + 1 < args.Length:
                    fixturesDir = args[++i];
                    break;
                case "--speed" when i + 1 < args.Length:
                    speed = args[++i].ToLowerInvariant() switch
                    {
                        "instant" => ReplaySpeed.Instant,
                        "realtime" => ReplaySpeed.RealTime,
                        "fixed" => ReplaySpeed.Fixed,
                        _ => ReplaySpeed.Instant
                    };
                    break;
                case "--interval" when i + 1 < args.Length:
                    intervalMs = int.TryParse(args[++i], out var ms) ? ms : 1000;
                    break;
                case "--http" when i + 1 < args.Length:
                    http = args[++i];
                    break;
                case "--print-state":
                    printState = true;
                    break;
            }
        }

        return fixturesDir is null ? null : new CliArgs(fixturesDir, speed, intervalMs, http, printState);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage: DotaAiCoach.ReplaySimulator --fixtures <dir> [--speed instant|realtime|fixed] [--interval 1000] [--http http://127.0.0.1:3000/gsi] [--print-state]");
    }
}
