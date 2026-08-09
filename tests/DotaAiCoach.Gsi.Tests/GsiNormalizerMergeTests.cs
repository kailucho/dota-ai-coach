using DotaAiCoach.Domain.Match;
using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class GsiNormalizerMergeTests
{
    private readonly GsiPayloadParser _parser = new();
    private readonly GsiNormalizer _normalizer = new(new GsiOptions());

    private static string ReadFixture(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", relativePath));

    [Fact]
    public void Merging_player_only_payload_leaves_previously_populated_hero_untouched()
    {
        var state = new MatchState();
        var full = _parser.Parse(ReadFixture("single/full-payload.json")).Payload!;
        _normalizer.Merge(state, full, DateTimeOffset.UtcNow);

        state.Hero.Level.Should().Be(10);

        var playerOnly = _parser.Parse(ReadFixture("single/partial-player-only.json")).Payload!;
        _normalizer.Merge(state, playerOnly, DateTimeOffset.UtcNow.AddSeconds(1));

        state.Hero.Level.Should().Be(10, "a payload with no hero section must not clear previously-known hero data");
        state.Player.Kills.Should().Be(5, "the player section was present and should update");
    }

    [Fact]
    public void A_present_section_with_an_absent_field_retains_the_prior_field_value()
    {
        var state = new MatchState();
        var full = _parser.Parse(ReadFixture("single/full-payload.json")).Payload!;
        _normalizer.Merge(state, full, DateTimeOffset.UtcNow);

        state.Hero.BuybackCost.Should().Be(400);

        // missing-buyback-fields.json has a hero section present, but no buyback fields.
        var missingBuyback = _parser.Parse(ReadFixture("single/missing-buyback-fields.json")).Payload!;
        _normalizer.Merge(state, missingBuyback, DateTimeOffset.UtcNow.AddSeconds(1));

        state.Hero.BuybackCost.Should().Be(400, "an absent field within a present section must not overwrite the prior value");
        state.Hero.Level.Should().Be(8, "fields that ARE present in the new section should update");
    }

    [Fact]
    public void A_slot_transitioning_to_empty_clears_it()
    {
        var state = new MatchState();
        var full = _parser.Parse(ReadFixture("single/full-payload.json")).Payload!;
        _normalizer.Merge(state, full, DateTimeOffset.UtcNow);

        state.Items.Inventory[0].Name.Should().Be("item_power_treads");

        var clearedJson = """
        {
          "provider": { "name": "Dota 2", "appid": 570, "version": 47, "timestamp": 1700000099 },
          "items": { "slot0": { "name": "empty" } }
        }
        """;
        var cleared = _parser.Parse(clearedJson).Payload!;
        _normalizer.Merge(state, cleared, DateTimeOffset.UtcNow.AddSeconds(1));

        state.Items.Inventory[0].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Item_in_slot8_is_correctly_merged_not_truncated()
    {
        // Regression test: found via live-client capture. GSI's actual
        // inventory has 9 slots (slot0-slot8), confirmed against a real
        // payload — an earlier 6-slot assumption silently dropped any
        // item placed in slot6, slot7, or slot8.
        var state = new MatchState();
        var payload = _parser.Parse("""{"items":{"slot8":{"name":"item_gem"}}}""").Payload!;

        _normalizer.Merge(state, payload, DateTimeOffset.UtcNow);

        state.Items.Inventory.Should().HaveCount(9);
        state.Items.Inventory[8].Name.Should().Be("item_gem");
        state.Items.OwnedItemNames().Should().Contain("item_gem");
    }

    [Fact]
    public void Second_neutral_slot_is_correctly_merged()
    {
        // Regression test: GSI exposes neutral0 AND neutral1 (an earlier
        // model only had a single Neutral slot).
        var state = new MatchState();
        var payload = _parser.Parse("""{"items":{"neutral1":{"name":"item_paladin_sword"}}}""").Payload!;

        _normalizer.Merge(state, payload, DateTimeOffset.UtcNow);

        state.Items.Neutral[1].Name.Should().Be("item_paladin_sword");
    }

    [Fact]
    public void Merging_identical_payload_twice_produces_zero_events_the_second_time()
    {
        var state = new MatchState();
        var payload = _parser.Parse(ReadFixture("single/full-payload.json")).Payload!;

        var first = _normalizer.Merge(state, payload, DateTimeOffset.UtcNow);
        first.Events.Should().NotBeEmpty("the first merge into empty state should derive at least the game-state transition");

        var second = _normalizer.Merge(state, payload, DateTimeOffset.UtcNow.AddSeconds(1));
        second.Events.Should().BeEmpty("merging an identical payload must be idempotent with respect to event derivation");
    }
}
