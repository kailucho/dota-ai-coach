using System.Collections.Immutable;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.MlInference;
using DotaAiCoach.RecommendationEngines;
using FluentAssertions;

namespace DotaAiCoach.Host.Tests;

/// <summary>
/// End-to-end smoke test for the retrained item-ranker (avg_rank_tier
/// feature added -- see docs/ROADMAP.md) using the ACTUAL bundled ONNX
/// model and popularity table, not a fake predictor. Exercises the same
/// path CoachSession/NextItemRule use live, so a broken bundled model or
/// a feature-shape mismatch between the C# predictor and the ONNX graph
/// surfaces here rather than only being caught manually in the overlay.
/// </summary>
public class LiveItemPlanSmokeTests
{
    [Fact]
    public void Bundled_item_ranker_and_popularity_table_load_and_produce_a_multi_phase_item_plan()
    {
        using var itemRankPredictor = new ItemRankPredictor();
        var itemPopularity = HeroItemPopularityTable.LoadFromBundledModels();

        // A representative live-shaped feature set: Pudge (hero_id=14, a
        // real ID with training data), 12 minutes in, 2 items owned, no
        // known rank tier yet (exercises the AverageRankTierFill path).
        var features = new LiveFeatureSet
        {
            IsUsable = true,
            FeedIsStale = false,
            GameState = GameState.GameInProgress,
            HeroName = "Pudge",
            HeroId = 14,
            GameMinutes = 12,
            Kills = 2,
            Deaths = 1,
            Assists = 3,
            TotalGold = 2200,
            Gpm = 350,
            Xpm = 400,
            LastHits = 60,
            Denies = 5,
            HasBuybackAvailable = true,
            BuybackCost = 400,
            OwnedItems = new[] { "item_boots", "item_magic_wand" }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
            OwnedItemCount = 2,
        };

        var byPhase = NextItemSelector.SelectByPhase(
            features,
            ItemCatalog.Default,
            itemRankPredictor,
            itemPopularity,
            enemyTeamAheadOnKills: false,
            averageRankTier: null); // exercises the AverageRankTierFill fallback path

        byPhase.Should().NotBeEmpty("the retrained item-ranker should still produce a real item plan for a known hero");
        byPhase.Select(p => p.Phase).Distinct().Should().HaveCountGreaterThan(1, "a real build should span more than one phase (Early/Mid/Late/Situational)");
        byPhase.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.ItemId));
    }

    [Fact]
    public void Bundled_item_ranker_produces_a_different_score_for_a_known_rank_tier_sensitive_input()
    {
        // avg_rank_tier has low but non-zero feature importance (see
        // item_ranker.py's training log: importance 11, weakest of 6
        // features) -- most random feature combinations are NOT sensitive
        // to it (independently verified: only ~16% of 200 sampled random
        // combos changed score at all when rank tier moved from Herald to
        // Immortal). This specific combination was independently verified
        // on the Python side (same model.pkl, same feature vector) to be
        // one that IS sensitive, so this is a real regression guard, not
        // a flaky assertion against an arbitrary point.
        using var itemRankPredictor = new ItemRankPredictor();

        var lowTierScore = itemRankPredictor.Predict(new DotaAiCoach.Domain.MlInference.ItemRankInput
        {
            MinuteOfMatch = 14.2,
            ItemsOwnedBeforeCount = 1,
            AverageRankTier = 11, // Herald
            ItemPopularityForHero = 0.186,
            ItemPopularityForHeroMinuteBucket = 0.346,
            ItemGlobalPopularity = 0.397,
        });

        var highTierScore = itemRankPredictor.Predict(new DotaAiCoach.Domain.MlInference.ItemRankInput
        {
            MinuteOfMatch = 14.2,
            ItemsOwnedBeforeCount = 1,
            AverageRankTier = 75, // Immortal
            ItemPopularityForHero = 0.186,
            ItemPopularityForHeroMinuteBucket = 0.346,
            ItemGlobalPopularity = 0.397,
        });

        lowTierScore.RankScore.Should().NotBe(highTierScore.RankScore,
            "avg_rank_tier must actually reach the ONNX model and affect scoring for input combinations the Python-side model treats as rank-tier-sensitive");
    }
}
