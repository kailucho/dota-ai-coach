using DotaAiCoach.Domain.MlInference;

namespace DotaAiCoach.RecommendationEngines.Tests;

internal sealed class FakeHeroItemPopularityLookup : IHeroItemPopularityLookup
{
    private readonly IReadOnlyDictionary<string, double> _scoreByItem;

    public FakeHeroItemPopularityLookup(IReadOnlyDictionary<string, double> scoreByItem)
    {
        _scoreByItem = scoreByItem;
    }

    // Score is smuggled through HeroPopularity (the field the fake
    // predictor below reads back out) so a per-item ranking can be
    // configured purely through this lookup, without the predictor
    // needing to know item identity.
    public double GlobalPopularity(string itemName) => 0.0;
    public double HeroPopularity(int heroId, string itemName) => _scoreByItem.TryGetValue(itemName, out var v) ? v : 0.0;
    public double HeroMinuteBucketPopularity(int heroId, string minuteBucket, string itemName) => 0.0;
}

/// <summary>Ranks candidates by whatever ItemPopularityForHero the caller supplied -- lets a test control the winner via FakeHeroItemPopularityLookup alone.</summary>
internal sealed class RanksByHeroPopularityPredictor : IItemRankPredictor
{
    public double AverageRankTierFill => 0.0;
    public ItemRankOutput Predict(ItemRankInput input) => new() { RankScore = input.ItemPopularityForHero };
}

internal sealed class FakeWinProbabilityPredictor : IWinProbabilityPredictor
{
    private readonly double _calibratedRadiantWinProbability;
    public WinProbabilityInput? LastInput { get; private set; }

    public FakeWinProbabilityPredictor(double calibratedRadiantWinProbability)
    {
        _calibratedRadiantWinProbability = calibratedRadiantWinProbability;
    }

    public WinProbabilityOutput Predict(WinProbabilityInput input)
    {
        LastInput = input;
        return new WinProbabilityOutput
        {
            RawRadiantWinProbability = _calibratedRadiantWinProbability,
            CalibratedRadiantWinProbability = _calibratedRadiantWinProbability
        };
    }
}
