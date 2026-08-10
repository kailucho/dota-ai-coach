using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

/// <summary>
/// True numeric parity tests against real Python-generated fixtures
/// (docs/adr/0024) -- see WinProbabilityPredictorParityTests for the
/// full rationale (same pattern, applied to draft-baseline).
/// </summary>
public class DraftScorePredictorParityTests
{
    private const double OnnxParityTolerance = 1e-4;
    private const double CalibrationParityTolerance = 1e-6;

    private static string ModelDir => Path.Combine(
        AppContext.BaseDirectory, "Models", "draft-baseline");

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "OnnxParity", "draft_baseline_parity.json");

    private static DraftScorePredictor MakePredictor() =>
        new(
            Path.Combine(ModelDir, "model.onnx"),
            Path.Combine(ModelDir, "onnx_export_metadata.json"));

    private static DraftScoreInput ToInput(Dictionary<string, double> f) => new()
    {
        HeroWinrateDifference = f["hero_winrate_diff"],
        MatchupAdvantage = f["matchup_advantage"],
    };

    public static IEnumerable<object[]> Cases()
    {
        var fixture = OnnxParityFixture.Load(FixturePath);
        foreach (var c in fixture.Cases)
        {
            yield return new object[] { c };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Predict_reproduces_the_exact_onnx_output_recorded_by_the_python_fixture(OnnxParityCase testCase)
    {
        using var predictor = MakePredictor();

        var result = predictor.Predict(ToInput(testCase.Input));

        result.RawRadiantWinProbability.Should().BeApproximately(
            testCase.ExpectedOnnxOutput, OnnxParityTolerance,
            because: $"case '{testCase.CaseName}': {testCase.Description}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Predict_calibrated_output_matches_the_python_isotonic_calibrator(OnnxParityCase testCase)
    {
        testCase.ExpectedCalibratedOutput.Should().NotBeNull();

        using var predictor = MakePredictor();

        var result = predictor.Predict(ToInput(testCase.Input));

        result.CalibratedRadiantWinProbability.Should().BeApproximately(
            testCase.ExpectedCalibratedOutput!.Value, CalibrationParityTolerance,
            because: $"case '{testCase.CaseName}': {testCase.Description}");
    }

    [Fact]
    public void Fixture_metadata_records_a_measured_not_assumed_tolerance()
    {
        var fixture = OnnxParityFixture.Load(FixturePath);

        fixture.Metadata.SourceModelId.Should().Be("draft-baseline-v1.0-20260810T032042Z");
        fixture.Metadata.MaxAbsDiffRawVsOnnxAchieved.Should().BeLessThan(OnnxParityTolerance);
        fixture.Cases.Should().HaveCount(fixture.Metadata.NCases);
    }
}
