using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.RecommendationEngines;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.FairPlay.Tests;

file sealed class NeutralItemRankPredictor : IItemRankPredictor
{
    public double AverageRankTierFill => 0.0;
    public ItemRankOutput Predict(ItemRankInput input) => new() { RankScore = 0.0 };
}

file sealed class EmptyPopularityLookup : IHeroItemPopularityLookup
{
    public double GlobalPopularity(string itemName) => 0.0;
    public double HeroPopularity(int heroId, string itemName) => 0.0;
    public double HeroMinuteBucketPopularity(int heroId, string minuteBucket, string itemName) => 0.0;
}

file sealed class NeutralWinProbabilityPredictor : IWinProbabilityPredictor
{
    public WinProbabilityOutput Predict(WinProbabilityInput input) =>
        new() { RawRadiantWinProbability = 0.5, CalibratedRadiantWinProbability = 0.5 };
}

/// <summary>
/// Per dota-fairplay's testing guidance: feeding a LiveFeatureSet with
/// every nullable at null should produce an empty, non-throwing
/// recommendation list — proving the engine doesn't implicitly depend on
/// data it shouldn't have, rather than silently fabricating output from
/// absent input.
/// </summary>
public class DegradedInputTests
{
    [Fact]
    public void Fully_unusable_feature_set_produces_no_recommendations_without_throwing()
    {
        var engine = new RuleRecommendationEngine(new IRule[]
        {
            new NextItemRule(), new AffordabilityRule(), new BuybackWarningRule(), new PowerSpikeRule(), new WinProbabilityRule()
        });

        var unusable = LiveFeatureSet.Unusable(GameState.Unknown, stale: true);

        var act = () => engine.Evaluate(unusable);

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void Usable_but_entirely_empty_feature_set_does_not_throw()
    {
        // IsUsable = true but every other nullable field left at its
        // default (null) — the engine and its rules must degrade
        // gracefully (return null / skip), not throw or fabricate values.
        var features = new LiveFeatureSet
        {
            IsUsable = true,
            FeedIsStale = false,
            GameState = GameState.GameInProgress
        };

        var engine = new RuleRecommendationEngine(new IRule[]
        {
            new NextItemRule(), new AffordabilityRule(), new BuybackWarningRule(), new PowerSpikeRule(), new WinProbabilityRule()
        });

        var act = () => engine.Evaluate(features);

        act.Should().NotThrow();
    }

    [Fact]
    public void Usable_but_empty_feature_set_does_not_throw_with_ML_predictors_wired()
    {
        // Same as above, but with real ML predictors wired through the
        // engine constructor -- proves the ML path also degrades
        // gracefully (HeroId/Team/GameMinutes all null here) rather than
        // NRE-ing when required inputs are missing.
        var features = new LiveFeatureSet
        {
            IsUsable = true,
            FeedIsStale = false,
            GameState = GameState.GameInProgress
        };

        var engine = new RuleRecommendationEngine(
            new IRule[] { new NextItemRule(), new AffordabilityRule(), new WinProbabilityRule() },
            itemRankPredictor: new NeutralItemRankPredictor(),
            itemPopularity: new EmptyPopularityLookup(),
            winProbabilityPredictor: new NeutralWinProbabilityPredictor());

        var act = () => engine.Evaluate(features);

        // NextItemRule/AffordabilityRule correctly fall back to their
        // non-ML progression (HeroId is null, so the ML path never
        // engages) and can legitimately still produce output; the
        // ML-only WinProbabilityRule (Team/GameMinutes both null) must
        // not. The key assertion is "does not throw" -- degradation,
        // not silent fabrication.
        act.Should().NotThrow();
        act().Should().NotContain(r => r.RuleId == "win-probability");
    }
}
