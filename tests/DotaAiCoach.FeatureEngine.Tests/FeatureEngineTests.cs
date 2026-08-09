using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using FluentAssertions;

namespace DotaAiCoach.FeatureEngine.Tests;

public class FeatureEngineTests
{
    private readonly FeatureEngine _engine = new();

    private static MatchState StateInProgress(
        PlayerSnapshot? player = null,
        HeroSnapshot? hero = null,
        ItemsSnapshot? items = null,
        int? clockTime = 600) => new()
    {
        Match = MatchSnapshot.Empty with { GameState = GameState.GameInProgress, ClockTime = clockTime },
        Player = player ?? PlayerSnapshot.Empty,
        Hero = hero ?? HeroSnapshot.Empty,
        Items = items ?? ItemsSnapshot.Empty
    };

    private static FeedHealth LiveHealth() => new() { Status = FeedStatus.Live };

    [Fact]
    public void IsUsable_is_false_when_feed_is_stale()
    {
        var state = StateInProgress();
        var stale = new FeedHealth { Status = FeedStatus.Stale };

        var features = _engine.Build(state, stale, Array.Empty<DomainEvent>());

        features.IsUsable.Should().BeFalse();
        features.FeedIsStale.Should().BeTrue();
    }

    [Fact]
    public void IsUsable_is_false_when_game_state_is_not_in_progress()
    {
        var state = new MatchState { Match = MatchSnapshot.Empty with { GameState = GameState.HeroSelection } };

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.IsUsable.Should().BeFalse();
    }

    [Fact]
    public void GameMinutes_is_derived_from_clock_seconds()
    {
        var state = StateInProgress(clockTime: 600);

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.GameMinutes.Should().Be(10.0);
    }

    [Fact]
    public void LastHitsPerMinute_is_null_when_clock_is_unknown_no_fabricated_zero()
    {
        var state = StateInProgress(player: PlayerSnapshot.Empty with { LastHits = 20 }, clockTime: null);

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.LastHitsPerMinute.Should().BeNull();
    }

    [Fact]
    public void HasBuybackAvailable_is_null_when_buyback_cost_is_unknown()
    {
        var state = StateInProgress(player: PlayerSnapshot.Empty with { Gold = 2000 });

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.HasBuybackAvailable.Should().BeNull();
    }

    [Theory]
    [InlineData(2000, 1500, 0, true)]   // enough gold, off cooldown
    [InlineData(1000, 1500, 0, false)]  // not enough gold
    [InlineData(2000, 1500, 30, false)] // on cooldown
    public void HasBuybackAvailable_reflects_gold_and_cooldown(int gold, int cost, int cooldown, bool expected)
    {
        var state = StateInProgress(
            player: PlayerSnapshot.Empty with { Gold = gold },
            hero: HeroSnapshot.Empty with { BuybackCost = cost, BuybackCooldown = cooldown });

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.HasBuybackAvailable.Should().Be(expected);
    }

    [Fact]
    public void GoldShortOfBuyback_is_null_when_affordable()
    {
        var state = StateInProgress(
            player: PlayerSnapshot.Empty with { Gold = 2000 },
            hero: HeroSnapshot.Empty with { BuybackCost = 1500, BuybackCooldown = 0 });

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.GoldShortOfBuyback.Should().BeNull();
    }

    [Fact]
    public void GoldShortOfBuyback_computes_the_deficit_when_not_affordable()
    {
        var state = StateInProgress(
            player: PlayerSnapshot.Empty with { Gold = 900 },
            hero: HeroSnapshot.Empty with { BuybackCost = 1500, BuybackCooldown = 0 });

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.GoldShortOfBuyback.Should().Be(600);
    }

    [Fact]
    public void OwnedItems_excludes_empty_slots_and_is_case_insensitive_lookup()
    {
        var items = ItemsSnapshot.Empty with
        {
            Inventory = new[]
            {
                new ItemSlot { Name = "item_blink" },
                ItemSlot.Empty, ItemSlot.Empty, ItemSlot.Empty, ItemSlot.Empty, ItemSlot.Empty
            }
        };
        var state = StateInProgress(items: items);

        var features = _engine.Build(state, LiveHealth(), Array.Empty<DomainEvent>());

        features.OwnedItemCount.Should().Be(1);
        features.OwnedItems.Contains("ITEM_BLINK").Should().BeTrue();
    }
}
