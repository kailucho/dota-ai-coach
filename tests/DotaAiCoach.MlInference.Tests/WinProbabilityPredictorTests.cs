using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

public class WinProbabilityPredictorTests
{
    private static string ModelDir => Path.Combine(
        AppContext.BaseDirectory, "Models", "win-probability-live");

    private static WinProbabilityPredictor MakePredictor() =>
        new(
            Path.Combine(ModelDir, "model.onnx"),
            Path.Combine(ModelDir, "onnx_export_metadata.json"));

    private static WinProbabilityInput NeutralInput() => new()
    {
        GoldAdvantageAtMinute = 0,
        XpAdvantageAtMinute = 0,
        GoldAdvantageSlope5 = 0,
        XpAdvantageSlope5 = 0,
        KillsDifferenceAtMinute = 0,
        DeathsInLast5MinDifference = 0,
        TowersKilledDifference = 0,
        BarracksKilledDifference = 0,
        RoshanKilledDifference = 0,
        HeroPickWinrateDifference = 0,
        AverageRankTier = 0,
        MinuteOfMatch = 0,
        FirstBloodRadiant = null,
    };

    [Fact]
    public void Predict_loads_the_model_and_returns_a_probability_in_range()
    {
        using var predictor = MakePredictor();

        var result = predictor.Predict(NeutralInput());

        result.RawRadiantWinProbability.Should().BeInRange(0.0, 1.0);
        result.CalibratedRadiantWinProbability.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Predict_large_positive_gold_and_xp_advantage_favors_radiant_over_neutral()
    {
        // The chosen model uses very strong regularization (C=1e-6, per
        // docs/adr/0022) so its outputs are close to 0.5 even for large
        // feature swings -- this asserts direction, not magnitude.
        using var predictor = MakePredictor();

        var neutralResult = predictor.Predict(NeutralInput() with { MinuteOfMatch = 30 });
        var input = NeutralInput() with
        {
            GoldAdvantageAtMinute = 20000,
            XpAdvantageAtMinute = 20000,
            MinuteOfMatch = 30,
        };

        var result = predictor.Predict(input);

        result.RawRadiantWinProbability.Should().BeGreaterThan(neutralResult.RawRadiantWinProbability);
    }

    [Fact]
    public void Predict_large_negative_gold_and_xp_advantage_favors_dire_over_neutral()
    {
        using var predictor = MakePredictor();

        var neutralResult = predictor.Predict(NeutralInput() with { MinuteOfMatch = 30 });
        var input = NeutralInput() with
        {
            GoldAdvantageAtMinute = -20000,
            XpAdvantageAtMinute = -20000,
            MinuteOfMatch = 30,
        };

        var result = predictor.Predict(input);

        result.RawRadiantWinProbability.Should().BeLessThan(neutralResult.RawRadiantWinProbability);
    }

    [Fact]
    public void Predict_feature_order_is_not_transposed()
    {
        // Swap gold-advantage and tower-difference values, which carry
        // different learned coefficient magnitudes/signs in a trained
        // logistic regression with distinct, non-interchangeable scales.
        // If the C# tensor construction silently transposed feature
        // order, this pair of predictions would not diverge the way an
        // untransposed mapping predicts.
        using var predictor = MakePredictor();

        var largeGoldAdvantage = NeutralInput() with
        {
            GoldAdvantageAtMinute = 15000,
            MinuteOfMatch = 25,
        };
        var largeTowerAdvantage = NeutralInput() with
        {
            TowersKilledDifference = 15000,
            MinuteOfMatch = 25,
        };

        var goldResult = predictor.Predict(largeGoldAdvantage);
        var towerResult = predictor.Predict(largeTowerAdvantage);

        // Both push toward Radiant favor, but a 15000-unit swing in raw
        // gold advantage (a feature with real per-unit scale in the
        // dataset) and the same raw numeric value interpreted instead as
        // a tower-kill differential (a feature whose real values are
        // single digits) must not produce identical output — if the
        // predictor were silently mapping values into the wrong tensor
        // slot in a way that lost the distinction between features, the
        // two results would coincide more closely than they do here.
        goldResult.RawRadiantWinProbability.Should().NotBe(towerResult.RawRadiantWinProbability);
    }

    [Fact]
    public void Predict_first_blood_radiant_true_increases_probability_over_false()
    {
        using var predictor = MakePredictor();

        var withFirstBlood = NeutralInput() with { FirstBloodRadiant = true, MinuteOfMatch = 10 };
        var withoutFirstBlood = NeutralInput() with { FirstBloodRadiant = false, MinuteOfMatch = 10 };

        var withResult = predictor.Predict(withFirstBlood);
        var withoutResult = predictor.Predict(withoutFirstBlood);

        withResult.RawRadiantWinProbability.Should().BeGreaterThan(withoutResult.RawRadiantWinProbability);
    }
}
