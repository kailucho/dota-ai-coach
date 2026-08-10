using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

/// <summary>
/// True numeric parity tests against real Python-generated fixtures
/// (docs/adr/0024) -- closes the gap ADR 0023 Decision 6 explicitly
/// flagged: no fixture recorded actual input feature vectors, so C#
/// could previously only assert structural correctness (loads, in-range,
/// feature order not transposed), never exact-number parity.
///
/// Two tolerances are asserted separately and intentionally NOT merged:
/// - ONNX-vs-pre-export-model parity (raw output): this predictor's C#
///   ONNX Runtime call reproduces the exact ONNX output recorded by the
///   Python-side onnxruntime.InferenceSession call in the fixture.
/// - Calibration parity: applying this predictor's IsotonicCalibrator to
///   the recorded raw output reproduces Python's IsotonicRegression
///   output for the same input.
/// </summary>
public class WinProbabilityPredictorParityTests
{
    private const double OnnxParityTolerance = 1e-4; // matches export_onnx.py's own validation tolerance for this model
    private const double CalibrationParityTolerance = 1e-6; // isotonic interpolation is exact arithmetic, not floating-point-sensitive like the ONNX graph

    private static string ModelDir => Path.Combine(
        AppContext.BaseDirectory, "Models", "win-probability-live");

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "OnnxParity", "win_probability_live_parity.json");

    private static WinProbabilityPredictor MakePredictor() =>
        new(
            Path.Combine(ModelDir, "model.onnx"),
            Path.Combine(ModelDir, "onnx_export_metadata.json"));

    private static WinProbabilityInput ToInput(Dictionary<string, double> f) => new()
    {
        GoldAdvantageAtMinute = f["gold_adv_at_T"],
        XpAdvantageAtMinute = f["xp_adv_at_T"],
        GoldAdvantageSlope5 = f["gold_adv_slope_5"],
        XpAdvantageSlope5 = f["xp_adv_slope_5"],
        KillsDifferenceAtMinute = f["kills_diff_at_T"],
        DeathsInLast5MinDifference = f["deaths_in_last_5min_diff"],
        TowersKilledDifference = f["towers_killed_diff"],
        BarracksKilledDifference = f["barracks_killed_diff"],
        RoshanKilledDifference = f["roshan_killed_diff"],
        HeroPickWinrateDifference = f["hero_pick_winrate_diff"],
        AverageRankTier = f["avg_rank_tier"],
        MinuteOfMatch = f["minute_T"],
        // first_blood_radiant is encoded as a float in the fixture (1.0/0.0/0.5);
        // the predictor re-encodes bool? the same way, so round-trip it exactly.
        FirstBloodRadiant = f["first_blood_radiant"] switch
        {
            1.0 => true,
            0.0 => false,
            _ => null,
        },
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

        fixture.Metadata.SourceModelId.Should().Be("win-probability-live-v1.0-20260810T034501Z");
        fixture.Metadata.MaxAbsDiffRawVsOnnxAchieved.Should().BeLessThan(OnnxParityTolerance);
        fixture.Cases.Should().HaveCount(fixture.Metadata.NCases);
    }
}
