using DotaAiCoach.Domain.Match;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.RecommendationEngines;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.FairPlay.Tests;

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
            new NextItemRule(), new AffordabilityRule(), new BuybackWarningRule(), new PowerSpikeRule()
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
            new NextItemRule(), new AffordabilityRule(), new BuybackWarningRule(), new PowerSpikeRule()
        });

        var act = () => engine.Evaluate(features);

        act.Should().NotThrow();
    }
}
