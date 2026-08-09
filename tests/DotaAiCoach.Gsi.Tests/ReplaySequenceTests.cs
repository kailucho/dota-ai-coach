using DotaAiCoach.Domain.Events;
using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using DotaAiCoach.Gsi.Replay;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class ReplaySequenceTests
{
    private static string FixtureDir(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", name);

    private static GsiPipeline CreatePipeline() =>
        new(new GsiPayloadParser(), new GsiNormalizer(new GsiOptions()));

    [Fact]
    public async Task Laning_sequence_merges_partial_payloads_correctly_at_every_step()
    {
        // Fixtures in this sequence are a real capture from a live Dota 2
        // client (see Fixtures/Gsi/README.md) — values below are the
        // actual observed values, not hand-picked round numbers.
        var fixtures = FixtureSequenceLoader.Load(FixtureDir("laning-sequence"));
        var pipeline = CreatePipeline();
        var target = new InProcessReplayTarget(pipeline);
        var simulator = new GsiReplaySimulator(target, new ReplayOptions { Speed = ReplaySpeed.Instant });

        var steps = await simulator.RunToCompletionAsync(fixtures);

        steps.Should().HaveCount(6);

        // Step 3 (farm-tick) — real captured state mid-laning.
        steps[2].StateAfter.Hero.Level.Should().Be(1);
        steps[2].StateAfter.Player.Gold.Should().Be(69);

        // Step 5 (level-up) — exactly one HeroLeveledUp event, level 1 -> 2.
        steps[4].Events.OfType<HeroLeveledUp>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { FromLevel = 1, ToLevel = 2 });
        steps[4].StateAfter.Hero.Level.Should().Be(2);

        // Step 6 (item-purchased) — a real multi-item purchase (2x Branches
        // + Magic Wand recipe) lands in inventory, gold reflects the payload.
        steps[5].StateAfter.Items.Inventory[2].Name.Should().Be("item_branches");
        steps[5].StateAfter.Items.Inventory[3].Name.Should().Be("item_branches");
        steps[5].StateAfter.Items.Inventory[4].Name.Should().Be("item_recipe_magic_wand");
        steps[5].StateAfter.Player.Gold.Should().Be(112);
        steps[5].Events.OfType<ItemPurchased>().Should().HaveCount(3,
            "the real capture shows three items appearing in previously-empty slots in the same payload");
    }

    [Fact]
    public async Task Death_sequence_emits_HeroDiedOrRespawned_at_death_and_respawn_only()
    {
        // Real capture: 01-alive -> 02-death-payload -> 03-still-dead
        // (no alive change, must not emit a spurious event) -> 04-respawn.
        var fixtures = FixtureSequenceLoader.Load(FixtureDir("hero-death"));
        var pipeline = CreatePipeline();
        var target = new InProcessReplayTarget(pipeline);
        var simulator = new GsiReplaySimulator(target);

        var steps = await simulator.RunToCompletionAsync(fixtures);

        steps.Should().HaveCount(4);
        steps[0].Events.OfType<HeroDiedOrRespawned>().Should().BeEmpty("first payload establishes baseline alive state");
        steps[1].Events.OfType<HeroDiedOrRespawned>().Should().ContainSingle().Which.IsAlive.Should().BeFalse();
        steps[2].Events.OfType<HeroDiedOrRespawned>().Should().BeEmpty("still dead — alive did not change on this update");
        steps[3].Events.OfType<HeroDiedOrRespawned>().Should().ContainSingle().Which.IsAlive.Should().BeTrue();
    }

    [Fact]
    public async Task Buyback_sequence_emits_BuybackStatusChanged_when_buyback_is_used()
    {
        // Real capture: 01-alive -> 02-death (buyback_cooldown=0, available)
        // -> 03-still-dead -> 04-buyback-used-respawn
        // (buyback_cooldown=919, just spent). CanBuybackNow flips
        // true -> false across this transition (spending buyback makes it
        // unavailable again until the cooldown clears), which is the
        // opposite direction from the hand-built fixture this replaced,
        // but is what the real client actually captured.
        var fixtures = FixtureSequenceLoader.Load(FixtureDir("buyback"));
        var pipeline = CreatePipeline();
        var target = new InProcessReplayTarget(pipeline);
        var simulator = new GsiReplaySimulator(target);

        var steps = await simulator.RunToCompletionAsync(fixtures);

        steps.Should().HaveCount(4);
        steps[^1].Events.OfType<BuybackStatusChanged>().Should().ContainSingle()
            .Which.CanBuybackNow.Should().BeFalse();
    }
}
