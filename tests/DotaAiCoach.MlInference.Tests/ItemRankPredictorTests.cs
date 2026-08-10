using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

public class ItemRankPredictorTests
{
    private static string ModelDir => Path.Combine(
        AppContext.BaseDirectory, "Models", "item-ranker");

    private static ItemRankPredictor MakePredictor() =>
        new(Path.Combine(ModelDir, "model.onnx"), Path.Combine(ModelDir, "metadata.json"));

    [Fact]
    public void Predict_loads_the_model_and_returns_a_finite_score()
    {
        using var predictor = MakePredictor();

        var result = predictor.Predict(new ItemRankInput
        {
            MinuteOfMatch = 10,
            ItemsOwnedBeforeCount = 2,
            AverageRankTier = predictor.AverageRankTierFill,
            ItemPopularityForHero = 0.3,
            ItemPopularityForHeroMinuteBucket = 0.2,
            ItemGlobalPopularity = 0.1,
        });

        double.IsFinite(result.RankScore).Should().BeTrue();
    }

    [Fact]
    public void Predict_higher_popularity_features_change_the_score()
    {
        using var predictor = MakePredictor();

        var lowPopularity = new ItemRankInput
        {
            MinuteOfMatch = 15,
            ItemsOwnedBeforeCount = 3,
            AverageRankTier = predictor.AverageRankTierFill,
            ItemPopularityForHero = 0.01,
            ItemPopularityForHeroMinuteBucket = 0.01,
            ItemGlobalPopularity = 0.01,
        };
        var highPopularity = lowPopularity with
        {
            ItemPopularityForHero = 0.9,
            ItemPopularityForHeroMinuteBucket = 0.9,
            ItemGlobalPopularity = 0.9,
        };

        var lowResult = predictor.Predict(lowPopularity);
        var highResult = predictor.Predict(highPopularity);

        lowResult.RankScore.Should().NotBe(highResult.RankScore);
    }

    [Fact]
    public void Predict_feature_order_is_not_transposed()
    {
        // minute and n_items_owned_before occupy the first two tensor
        // slots; swapping large values between them and the popularity
        // slots should not produce identical scores if the mapping is
        // correct.
        using var predictor = MakePredictor();

        var largeMinute = new ItemRankInput
        {
            MinuteOfMatch = 60,
            ItemsOwnedBeforeCount = 0,
            AverageRankTier = predictor.AverageRankTierFill,
            ItemPopularityForHero = 0,
            ItemPopularityForHeroMinuteBucket = 0,
            ItemGlobalPopularity = 0,
        };
        var largeItemsOwned = new ItemRankInput
        {
            MinuteOfMatch = 0,
            ItemsOwnedBeforeCount = 60,
            AverageRankTier = predictor.AverageRankTierFill,
            ItemPopularityForHero = 0,
            ItemPopularityForHeroMinuteBucket = 0,
            ItemGlobalPopularity = 0,
        };

        var minuteResult = predictor.Predict(largeMinute);
        var itemsResult = predictor.Predict(largeItemsOwned);

        minuteResult.RankScore.Should().NotBe(itemsResult.RankScore);
    }
}
