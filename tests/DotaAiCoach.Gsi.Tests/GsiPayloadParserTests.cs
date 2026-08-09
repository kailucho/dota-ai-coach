using DotaAiCoach.Domain.Match;
using DotaAiCoach.Gsi.Parsing;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class GsiPayloadParserTests
{
    private readonly GsiPayloadParser _parser = new();

    private static string ReadFixture(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", relativePath));

    [Fact]
    public void Parse_full_payload_extracts_every_expected_field()
    {
        var json = ReadFixture("single/full-payload.json");

        var result = _parser.Parse(json);

        result.Success.Should().BeTrue();
        var payload = result.Payload!;

        payload.Provider.Should().NotBeNull();
        payload.Provider!.AppId.Should().Be(570);

        payload.Map.Should().NotBeNull();
        payload.Map!.MatchId.Should().Be("7654321098");
        payload.Map.ClockTime.Should().Be(720);
        payload.Map.GameState.Should().Be("DOTA_GAMERULES_STATE_GAME_IN_PROGRESS");

        payload.Player.Should().NotBeNull();
        payload.Player!.Kills.Should().Be(4);
        payload.Player.Gold.Should().Be(1850);
        payload.Player.Gpm.Should().Be(512);

        payload.Hero.Should().NotBeNull();
        payload.Hero!.Name.Should().Be("npc_dota_hero_juggernaut");
        payload.Hero.Level.Should().Be(10);
        payload.Hero.BuybackCost.Should().Be(400);

        payload.Items.Should().NotBeNull();
        payload.Items!.Slots.Should().ContainKey("slot0");
        payload.Items.Slots["slot0"].Name.Should().Be("item_power_treads");
        payload.Items.Slots.Should().ContainKey("teleport0");
        payload.Items.Slots["teleport0"].Name.Should().Be("item_tpscroll");

        payload.Abilities.Should().NotBeNull();
        payload.Abilities!.Slots.Should().ContainKey("ability3");
        payload.Abilities.Slots["ability3"].Ultimate.Should().BeTrue();
    }

    [Fact]
    public void Parse_partial_payload_leaves_absent_sections_null_not_defaulted()
    {
        var json = ReadFixture("single/partial-player-only.json");

        var result = _parser.Parse(json);

        result.Success.Should().BeTrue();
        result.Payload!.Player.Should().NotBeNull();
        result.Payload.Map.Should().BeNull();
        result.Payload.Hero.Should().BeNull();
        result.Payload.Items.Should().BeNull();
    }

    [Fact]
    public void Parse_malformed_json_fails_without_throwing()
    {
        var json = ReadFixture("single/malformed.json");

        var act = () => _parser.Parse(json);

        act.Should().NotThrow();
        var result = act();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Payload.Should().BeNull();
    }

    [Fact]
    public void Parse_empty_body_fails_without_throwing()
    {
        var result = _parser.Parse(string.Empty);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Parse_unknown_game_state_parses_successfully_and_degrades_to_unknown()
    {
        var json = ReadFixture("single/unknown-game-state.json");

        var result = _parser.Parse(json);

        result.Success.Should().BeTrue();
        var rawState = result.Payload!.Map!.GameState;
        rawState.Should().Be("DOTA_GAMERULES_STATE_FUTURE_THING_VALVE_ADDS");
        GameStateParser.Parse(rawState).Should().Be(GameState.Unknown);
    }

    [Fact]
    public void Parse_charges_variants_resolves_all_three_observed_keys()
    {
        var json = ReadFixture("single/charges-variants.json");

        var result = _parser.Parse(json);

        result.Success.Should().BeTrue();
        var items = result.Payload!.Items!.Slots;

        items["slot0"].EffectiveCharges.Should().Be(6);   // "charges"
        items["slot1"].EffectiveCharges.Should().Be(3);   // "item_charges"
        items["slot2"].EffectiveCharges.Should().Be(1);   // "ability_charges"
        items["slot3"].EffectiveCharges.Should().BeNull(); // none present
    }

    [Fact]
    public void Parse_missing_buyback_fields_leaves_them_null()
    {
        var json = ReadFixture("single/missing-buyback-fields.json");

        var result = _parser.Parse(json);

        result.Success.Should().BeTrue();
        result.Payload!.Hero!.BuybackCost.Should().BeNull();
        result.Payload.Hero.BuybackCooldown.Should().BeNull();
    }
}
