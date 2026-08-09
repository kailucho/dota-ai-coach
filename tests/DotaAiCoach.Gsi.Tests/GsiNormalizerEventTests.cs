using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class GsiNormalizerEventTests
{
    private readonly GsiPayloadParser _parser = new();
    private readonly GsiNormalizer _normalizer = new(new GsiOptions());

    [Fact]
    public void Level_up_emits_exactly_one_HeroLeveledUp_event()
    {
        var state = new MatchState();
        var level1 = _parser.Parse("""{"hero":{"level":1,"alive":true}}""").Payload!;
        _normalizer.Merge(state, level1, DateTimeOffset.UtcNow);

        var level2 = _parser.Parse("""{"hero":{"level":2}}""").Payload!;
        var result = _normalizer.Merge(state, level2, DateTimeOffset.UtcNow.AddSeconds(1));

        result.Events.OfType<HeroLeveledUp>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { FromLevel = 1, ToLevel = 2 });
    }

    [Fact]
    public void Item_appearing_in_empty_slot_emits_one_ItemPurchased_event()
    {
        var state = new MatchState();
        var empty = _parser.Parse("""{"items":{"slot0":{"name":"empty"}}}""").Payload!;
        _normalizer.Merge(state, empty, DateTimeOffset.UtcNow);

        var purchased = _parser.Parse("""{"items":{"slot0":{"name":"item_magic_wand"}}}""").Payload!;
        var result = _normalizer.Merge(state, purchased, DateTimeOffset.UtcNow.AddSeconds(1));

        result.Events.OfType<ItemPurchased>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { ItemName = "item_magic_wand", Location = ItemLocation.Inventory });
    }

    [Fact]
    public void Buyback_becoming_available_emits_BuybackStatusChanged()
    {
        var state = new MatchState();
        var onCooldown = _parser.Parse("""{"hero":{"buyback_cost":300,"buyback_cooldown":45}}""").Payload!;
        _normalizer.Merge(state, onCooldown, DateTimeOffset.UtcNow);

        var offCooldown = _parser.Parse("""{"hero":{"buyback_cost":300,"buyback_cooldown":0}}""").Payload!;
        var result = _normalizer.Merge(state, offCooldown, DateTimeOffset.UtcNow.AddSeconds(1));

        result.Events.OfType<BuybackStatusChanged>().Should().ContainSingle()
            .Which.CanBuybackNow.Should().BeTrue();
    }

    [Fact]
    public void Null_buyback_cost_never_fires_BuybackStatusChanged_undecidable_not_false()
    {
        var state = new MatchState();
        var noHeroYet = _parser.Parse("""{"player":{"gold":100}}""").Payload!;
        _normalizer.Merge(state, noHeroYet, DateTimeOffset.UtcNow);

        var stillNoBuybackCost = _parser.Parse("""{"hero":{"level":3,"alive":true}}""").Payload!;
        var result = _normalizer.Merge(state, stillNoBuybackCost, DateTimeOffset.UtcNow.AddSeconds(1));

        result.Events.OfType<BuybackStatusChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Death_and_respawn_each_emit_HeroDiedOrRespawned_and_nothing_else_does()
    {
        var state = new MatchState();
        var alive = _parser.Parse("""{"hero":{"alive":true}}""").Payload!;
        _normalizer.Merge(state, alive, DateTimeOffset.UtcNow);

        var died = _parser.Parse("""{"hero":{"alive":false,"respawn_seconds":25}}""").Payload!;
        var deathResult = _normalizer.Merge(state, died, DateTimeOffset.UtcNow.AddSeconds(1));
        deathResult.Events.OfType<HeroDiedOrRespawned>().Should().ContainSingle().Which.IsAlive.Should().BeFalse();

        var stillDead = _parser.Parse("""{"hero":{"respawn_seconds":10}}""").Payload!;
        var tickResult = _normalizer.Merge(state, stillDead, DateTimeOffset.UtcNow.AddSeconds(2));
        tickResult.Events.OfType<HeroDiedOrRespawned>().Should().BeEmpty("alive did not change on this update");

        var respawned = _parser.Parse("""{"hero":{"alive":true}}""").Payload!;
        var respawnResult = _normalizer.Merge(state, respawned, DateTimeOffset.UtcNow.AddSeconds(3));
        respawnResult.Events.OfType<HeroDiedOrRespawned>().Should().ContainSingle().Which.IsAlive.Should().BeTrue();
    }

    [Fact]
    public void EvaluateHealth_reports_stale_after_the_configured_threshold_with_no_payloads()
    {
        var options = new GsiOptions { StaleThreshold = TimeSpan.FromSeconds(45) };
        var normalizer = new GsiNormalizer(options);
        var state = new MatchState();

        var t0 = DateTimeOffset.UtcNow;
        var payload = _parser.Parse("""{"hero":{"level":1}}""").Payload!;
        normalizer.Merge(state, payload, t0);

        normalizer.EvaluateHealth(state, t0.AddSeconds(30)).Status.Should().Be(FeedStatus.Live);
        normalizer.EvaluateHealth(state, t0.AddSeconds(60)).Status.Should().Be(FeedStatus.Stale);
    }
}
