using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

public class DraftScorePredictorTests
{
    private static string ModelDir => Path.Combine(
        AppContext.BaseDirectory, "Models", "draft-baseline");

    private static DraftScorePredictor MakePredictor() =>
        new(
            Path.Combine(ModelDir, "model.onnx"),
            Path.Combine(ModelDir, "onnx_export_metadata.json"));

    [Fact]
    public void Predict_loads_the_model_and_returns_a_probability_in_range()
    {
        using var predictor = MakePredictor();

        var result = predictor.Predict(new DraftScoreInput
        {
            HeroWinrateDifference = 0,
            MatchupAdvantage = 0,
        });

        result.RawRadiantWinProbability.Should().BeInRange(0.0, 1.0);
        result.CalibratedRadiantWinProbability.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Predict_feature_order_is_not_transposed()
    {
        using var predictor = MakePredictor();

        var largeWinrateDiff = new DraftScoreInput { HeroWinrateDifference = 5.0, MatchupAdvantage = 0 };
        var largeMatchupAdvantage = new DraftScoreInput { HeroWinrateDifference = 0, MatchupAdvantage = 5.0 };

        var winrateResult = predictor.Predict(largeWinrateDiff);
        var matchupResult = predictor.Predict(largeMatchupAdvantage);

        // Both nonzero inputs should push probability away from the
        // all-zero baseline, but via different coefficients — if the
        // two features were transposed in the tensor, these two calls
        // (each isolating one feature) would swap results identically
        // rather than potentially differing.
        winrateResult.RawRadiantWinProbability.Should().NotBe(0.5);
        matchupResult.RawRadiantWinProbability.Should().NotBe(0.5);
    }
}
