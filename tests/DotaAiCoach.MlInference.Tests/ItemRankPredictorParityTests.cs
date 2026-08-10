using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

/// <summary>
/// True numeric parity tests against real Python-generated fixtures
/// (docs/adr/0024). No calibration step for this model (ranking task,
/// per item_ranker.py) -- only ONNX-vs-pre-export-model raw score parity
/// is asserted.
/// </summary>
public class ItemRankPredictorParityTests
{
    private const double OnnxParityTolerance = 1e-3; // matches export_onnx.py's own validation tolerance for this LightGBM model

    private static string ModelDir => Path.Combine(
        AppContext.BaseDirectory, "Models", "item-ranker");

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "OnnxParity", "item_ranker_parity.json");

    private static ItemRankPredictor MakePredictor() =>
        new(Path.Combine(ModelDir, "model.onnx"), Path.Combine(ModelDir, "metadata.json"));

    private static ItemRankInput ToInput(Dictionary<string, double> f) => new()
    {
        MinuteOfMatch = f["minute"],
        ItemsOwnedBeforeCount = f["n_items_owned_before"],
        AverageRankTier = f["avg_rank_tier"],
        ItemPopularityForHero = f["item_popularity_for_hero"],
        ItemPopularityForHeroMinuteBucket = f["item_popularity_for_hero_minute_bucket"],
        ItemGlobalPopularity = f["item_global_popularity"],
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

        result.RankScore.Should().BeApproximately(
            testCase.ExpectedOnnxOutput, OnnxParityTolerance,
            because: $"case '{testCase.CaseName}': {testCase.Description}");
    }

    [Fact]
    public void Fixture_metadata_records_a_measured_not_assumed_tolerance()
    {
        var fixture = OnnxParityFixture.Load(FixturePath);

        fixture.Metadata.SourceModelId.Should().Be("item-ranker-v1.0-20260810T203622Z");
        fixture.Metadata.MaxAbsDiffRawVsOnnxAchieved.Should().BeLessThan(OnnxParityTolerance);
        fixture.Cases.Should().HaveCount(fixture.Metadata.NCases);
        fixture.Cases.Should().OnlyContain(c => c.ExpectedCalibratedOutput == null,
            "item-ranker is a ranking model with no calibration step (item_ranker.py)");
    }
}
