using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class WinProbabilityRuleTests
{
    private readonly WinProbabilityRule _rule = new();

    [Fact]
    public void Returns_null_when_no_predictor_is_wired()
    {
        var context = new RuleContext { Now = DateTimeOffset.UtcNow };
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        var result = _rule.Evaluate(features, context);

        result.Should().BeNull();
    }

    [Fact]
    public void Returns_null_when_team_is_unknown()
    {
        var context = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = new FakeWinProbabilityPredictor(0.7) };
        var features = TestFeatures.Usable(gameMinutes: 10, team: null);

        var result = _rule.Evaluate(features, context);

        result.Should().BeNull();
    }

    [Fact]
    public void Shows_the_players_own_side_probability_not_always_radiant()
    {
        var predictor = new FakeWinProbabilityPredictor(calibratedRadiantWinProbability: 0.7);
        var context = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = predictor };

        var radiantFeatures = TestFeatures.Usable(gameMinutes: 10, team: "radiant");
        var radiantResult = _rule.Evaluate(radiantFeatures, context);
        radiantResult!.Message.Should().Contain("70%");

        var direFeatures = TestFeatures.Usable(gameMinutes: 10, team: "dire");
        var direResult = _rule.Evaluate(direFeatures, context);
        direResult!.Message.Should().Contain("30%");
    }

    [Fact]
    public void Passes_real_kills_difference_and_zero_fill_for_unavailable_features()
    {
        var predictor = new FakeWinProbabilityPredictor(0.5);
        var context = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = predictor };
        var features = TestFeatures.Usable(gameMinutes: 12, team: "radiant", radiantScore: 10, direScore: 4);

        _rule.Evaluate(features, context);

        predictor.LastInput!.KillsDifferenceAtMinute.Should().Be(6.0);
        predictor.LastInput!.MinuteOfMatch.Should().Be(12.0);
        predictor.LastInput!.GoldAdvantageAtMinute.Should().Be(0.0);
        predictor.LastInput!.TowersKilledDifference.Should().Be(0.0);
        predictor.LastInput!.RoshanKilledDifference.Should().Be(0.0);
    }

    [Fact]
    public void Is_P2_and_carries_the_ML_reason_code()
    {
        var context = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = new FakeWinProbabilityPredictor(0.5) };
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        var result = _rule.Evaluate(features, context);

        result!.Priority.Should().Be(MessagePriority.P2);
        result.ReasonCodes.Should().Contain(ReasonCode.MlWinProbabilityEstimate);
    }

    [Fact]
    public void Passes_the_contexts_AverageRankTier_when_known()
    {
        var predictor = new FakeWinProbabilityPredictor(0.5);
        var context = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = predictor, AverageRankTier = 63 };
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        _rule.Evaluate(features, context);

        predictor.LastInput!.AverageRankTier.Should().Be(63.0);
    }

    [Fact]
    public void Zero_fills_AverageRankTier_when_unknown_rather_than_fabricating()
    {
        var predictor = new FakeWinProbabilityPredictor(0.5);
        var context = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = predictor, AverageRankTier = null };
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        _rule.Evaluate(features, context);

        predictor.LastInput!.AverageRankTier.Should().Be(0.0);
    }

    [Fact]
    public void MessageKey_is_stable_within_the_same_5_point_band()
    {
        var context61 = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = new FakeWinProbabilityPredictor(0.61) };
        var context63 = new RuleContext { Now = DateTimeOffset.UtcNow, WinProbabilityPredictor = new FakeWinProbabilityPredictor(0.63) };
        var features = TestFeatures.Usable(gameMinutes: 10, team: "radiant");

        var result61 = _rule.Evaluate(features, context61);
        var result63 = _rule.Evaluate(features, context63);

        result61!.MessageKey.Should().Be(result63!.MessageKey);
    }
}
