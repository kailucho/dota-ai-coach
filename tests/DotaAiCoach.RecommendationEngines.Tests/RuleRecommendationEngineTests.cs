using DotaAiCoach.Domain.Match;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class RuleRecommendationEngineTests
{
    private static readonly IRule[] AllRules = { new NextItemRule(), new AffordabilityRule(), new BuybackWarningRule(), new PowerSpikeRule() };

    [Fact]
    public void Returns_empty_when_features_are_not_usable_stale_suppression()
    {
        var engine = new RuleRecommendationEngine(AllRules);
        var unusable = LiveFeatureSet.Unusable(GameState.HeroSelection, stale: true);

        var result = engine.Evaluate(unusable);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Orders_by_priority_P0_before_P1_before_P2()
    {
        var engine = new RuleRecommendationEngine(AllRules);
        var features = TestFeatures.Usable(
            gold: 0, gpm: 300, hasBuyback: false, gameMinutes: 25, isAlive: false);

        var result = engine.Evaluate(features);

        result.Should().NotBeEmpty();
        var priorities = result.Select(r => r.Priority).ToArray();
        priorities.Should().BeInAscendingOrder();
    }

    private sealed class ThrowingRule : IRule
    {
        public string RuleId => "throwing";
        public MessagePriority Priority => MessagePriority.P1;
        public Recommendation? Evaluate(LiveFeatureSet features, RuleContext context) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public void A_throwing_rule_is_isolated_and_others_still_produce_output()
    {
        var rules = new IRule[] { new ThrowingRule(), new NextItemRule() };
        var engine = new RuleRecommendationEngine(rules);
        var features = TestFeatures.Usable(gold: 0);

        var act = () => engine.Evaluate(features);

        act.Should().NotThrow();
        act().Should().ContainSingle(r => r.RuleId == "next-item");
    }

    [Fact]
    public void Constructor_wired_ML_predictors_flow_through_to_rules_via_RuleContext()
    {
        var winProbabilityPredictor = new FakeWinProbabilityPredictor(0.8);
        var engine = new RuleRecommendationEngine(
            new IRule[] { new WinProbabilityRule() },
            winProbabilityPredictor: winProbabilityPredictor);
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        var result = engine.Evaluate(features);

        result.Should().ContainSingle(r => r.RuleId == "win-probability" && r.Message.Contains("80%"));
    }

    [Fact]
    public void SetAverageRankTier_propagates_to_WinProbabilityRule_on_the_next_Evaluate()
    {
        var predictor = new FakeWinProbabilityPredictor(0.5);
        var engine = new RuleRecommendationEngine(
            new IRule[] { new WinProbabilityRule() },
            winProbabilityPredictor: predictor);
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        engine.SetAverageRankTier(72);
        engine.Evaluate(features);

        predictor.LastInput!.AverageRankTier.Should().Be(72.0);
    }
}
