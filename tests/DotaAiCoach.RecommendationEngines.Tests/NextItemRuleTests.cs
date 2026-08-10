using DotaAiCoach.Domain.Recommendations;
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

    [Fact]
    public void Never_suggests_two_mutually_exclusive_boots_in_the_same_ItemPlan()
    {
        // Regression test: reported against a live overlay screenshot
        // showing Power Treads, Phase Boots, AND Arcane Boots all
        // suggested together in the Mid phase -- a player can only ever
        // equip one boot upgrade, so showing all three as if they were
        // independent picks is never a real choice. Give every boot
        // upgrade an artificially high, near-identical popularity so
        // without the fix all three would rank into the same phase's
        // top slots.
        var popularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>
        {
            ["item_power_treads"] = 0.90,
            ["item_phase_boots"] = 0.91,
            ["item_arcane_boots"] = 0.89
        });
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = popularity
        };
        var features = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: 42);

        var result = _rule.Evaluate(features, mlContext);

        var midItemIds = result!.ItemPlan!.Entries.Where(e => e.PhaseLabel == "Mid").Select(e => e.ItemId).ToList();
        var bootUpgradesShown = midItemIds.Count(id =>
            id is "item_power_treads" or "item_phase_boots" or "item_arcane_boots");

        bootUpgradesShown.Should().Be(1, "the three boot upgrades are mutually exclusive -- only the single highest-ranked one should ever appear");
        // The highest-popularity one (Phase Boots, 0.91) should be the one that won.
        midItemIds.Should().Contain("item_phase_boots");
    }

    [Fact]
    public void Uses_ML_ranking_over_the_full_catalog_when_a_predictor_is_wired()
    {
        // item_bfury (Late phase) never appears in the non-ML fallback's
        // ProgressionOrder top slot behavior, but IS in ItemCatalog.Default
        // -- proves the ML path ranks the full catalog, not just the
        // hand-ordered fallback list. It surfaces as the Late-phase
        // alternative rather than the primary ItemId, since NextItemRule
        // now always leads with the earliest phase's pick (see
        // NextItemSelector.SelectByPhase).
        var popularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>
        {
            ["item_bfury"] = 0.99
        });
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = popularity
        };
        var features = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: 42);

        var result = _rule.Evaluate(features, mlContext);

        result!.Alternatives.Should().Contain(a => a.ItemId == "item_bfury");
    }

    [Fact]
    public void Message_stays_short_and_single_item_per_dota_live_coach_while_ItemPlan_carries_all_phases()
    {
        // Regression guard: the live Message must never become a
        // multi-phase plan (violates dota-live-coach's short-fragment
        // rule) -- the full plan belongs only in ItemPlan, which the
        // overlay's always-visible panel reads separately from the
        // cooldown-gated message stream.
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>())
        };
        var features = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: 42);

        var result = _rule.Evaluate(features, mlContext);

        result!.Message.Length.Should().BeLessThanOrEqualTo(28);
        result.ItemPlan.Should().NotBeNull();
        // Every phase present in the catalog must contribute at least one
        // entry -- exact per-phase counts vary with catalog size (up to
        // NextItemSelector.MaxPerPhase each), so this checks phase
        // coverage, not a specific total.
        result.ItemPlan!.Entries.Select(e => e.PhaseLabel).Distinct()
            .Should().BeEquivalentTo(new[] { "Early", "Mid", "Late", "Situational" });
    }

    [Fact]
    public void Always_surfaces_several_suggestions_per_phase_when_ML_is_wired()
    {
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>())
        };
        var features = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: 42);

        var result = _rule.Evaluate(features, mlContext);

        // With the enlarged catalog, multiple phases have more than one
        // unsatisfied candidate -- Alternatives must carry all of them,
        // not just one per phase.
        result!.Alternatives.Count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void Boosts_BKB_and_utility_items_when_the_enemy_team_is_ahead_on_kills()
    {
        // With Bkb/Utility items boosted, item_black_king_bar (Late,
        // Bkb-tagged) should rank ABOVE item_desolator (Late,
        // Damage-only) within the Late phase's ItemPlan entries once the
        // enemy team is ahead, even though both start at the same base
        // popularity score. Both items still fit in the Late phase's
        // (up to 6) suggestions with the enlarged catalog, so this
        // asserts relative order, not exclusion.
        var popularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>
        {
            ["item_black_king_bar"] = 0.5,
            ["item_desolator"] = 0.5
        });
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = popularity
        };
        var behindFeatures = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: 42, team: "radiant", radiantScore: 2, direScore: 10);

        var result = _rule.Evaluate(behindFeatures, mlContext);

        var lateItemIds = result!.ItemPlan!.Entries.Where(e => e.PhaseLabel == "Late").Select(e => e.ItemId).ToList();
        lateItemIds.Should().Contain("item_black_king_bar").And.Contain("item_desolator");
        lateItemIds.IndexOf("item_black_king_bar").Should().BeLessThan(lateItemIds.IndexOf("item_desolator"));
    }

    [Fact]
    public void Does_not_boost_when_the_enemy_team_is_not_ahead_on_kills()
    {
        var popularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>
        {
            ["item_black_king_bar"] = 0.5,
            ["item_desolator"] = 0.51 // slightly ahead on its own merit, no boost should be applied to BKB to overturn it
        });
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = popularity
        };
        var evenFeatures = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: 42, team: "radiant", radiantScore: 5, direScore: 5);

        var result = _rule.Evaluate(evenFeatures, mlContext);

        var lateItemIds = result!.ItemPlan!.Entries.Where(e => e.PhaseLabel == "Late").Select(e => e.ItemId).ToList();
        lateItemIds.Should().Contain("item_black_king_bar").And.Contain("item_desolator");
        lateItemIds.IndexOf("item_desolator").Should().BeLessThan(lateItemIds.IndexOf("item_black_king_bar"));
        result.ReasonCodes.Should().NotContain(ReasonCode.EnemyTeamAheadOnKills);
    }

    [Fact]
    public void Falls_back_to_non_ML_progression_when_HeroId_is_unknown_even_if_predictor_is_wired()
    {
        var mlContext = new RuleContext
        {
            Now = DateTimeOffset.UtcNow,
            ItemRankPredictor = new RanksByHeroPopularityPredictor(),
            ItemPopularity = new FakeHeroItemPopularityLookup(new Dictionary<string, double>())
        };
        var features = TestFeatures.Usable(gold: 0, gameMinutes: 10, heroId: null);

        var result = _rule.Evaluate(features, mlContext);

        result!.ItemId.Should().Be("item_boots");
    }
}
