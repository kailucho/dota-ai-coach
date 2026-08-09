using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class NextItemRuleTests
{
    private readonly NextItemRule _rule = new();
    private readonly RuleContext _context = new() { Now = DateTimeOffset.UtcNow };

    [Fact]
    public void Never_recommends_an_owned_item()
    {
        var features = TestFeatures.Usable(gold: 0, ownedItems: new[] { "item_boots" });

        var result = _rule.Evaluate(features, _context);

        result!.ItemId.Should().NotBe("item_boots");
    }

    [Fact]
    public void Falls_through_the_chain_as_items_are_acquired()
    {
        var noItems = _rule.Evaluate(TestFeatures.Usable(gold: 0), _context);
        noItems!.ItemId.Should().Be("item_boots");

        var withBoots = _rule.Evaluate(TestFeatures.Usable(gold: 0, ownedItems: new[] { "item_boots" }), _context);
        withBoots!.ItemId.Should().Be("item_magic_wand");
    }

    [Fact]
    public void Returns_null_when_the_catalog_is_exhausted()
    {
        var everything = new[]
        {
            "item_boots", "item_magic_wand", "item_bracer", "item_phase_boots", "item_power_treads",
            "item_blink", "item_black_king_bar", "item_desolator", "item_manta", "item_bfury"
        };
        var features = TestFeatures.Usable(gold: 0, ownedItems: everything);

        var result = _rule.Evaluate(features, _context);

        result.Should().BeNull();
    }

    [Fact]
    public void Populates_alternatives()
    {
        var features = TestFeatures.Usable(gold: 0);

        var result = _rule.Evaluate(features, _context);

        result!.Alternatives.Should().NotBeEmpty();
        result.Alternatives.Should().NotContain(a => a.ItemId == result.ItemId);
    }

    [Theory]
    [InlineData("item_phase_boots")]
    [InlineData("item_power_treads")]
    public void Does_not_recommend_boots_when_an_upgraded_boots_item_is_already_owned(string upgradedBoots)
    {
        // Regression test: found against a live Dota client where the
        // player had upgraded Boots into Phase Boots (Dota consumes the
        // base item into the upgrade, so "item_boots" genuinely leaves
        // OwnedItems) and the engine kept recommending "Boots" anyway,
        // because it only checked for the exact item id, not the
        // upgrade path. See ItemDefinition.UpgradesTo.
        var features = TestFeatures.Usable(gold: 0, ownedItems: new[] { upgradedBoots });

        var result = _rule.Evaluate(features, _context);

        result!.ItemId.Should().NotBe("item_boots");
        result.Alternatives.Should().NotContain(a => a.ItemId == "item_boots");
    }

    [Fact]
    public void Does_not_recommend_power_treads_when_phase_boots_already_owned()
    {
        // Regression test: found against a live Dota client. Power Treads
        // and Phase Boots are mutually exclusive boot upgrades — a hero
        // can only ever equip one — but neither is "an upgrade of" the
        // other, so the UpgradesTo check alone didn't catch this; needed
        // the separate ExcludedByOwning relationship.
        var features = TestFeatures.Usable(gold: 0, ownedItems: new[] { "item_phase_boots" });

        var result = _rule.Evaluate(features, _context);

        result!.ItemId.Should().NotBe("item_power_treads");
        result.Alternatives.Should().NotContain(a => a.ItemId == "item_power_treads");
    }

    [Fact]
    public void Does_not_recommend_phase_boots_when_power_treads_already_owned()
    {
        var features = TestFeatures.Usable(gold: 0, ownedItems: new[] { "item_power_treads" });

        var result = _rule.Evaluate(features, _context);

        result!.ItemId.Should().NotBe("item_phase_boots");
        result.Alternatives.Should().NotContain(a => a.ItemId == "item_phase_boots");
    }
}
