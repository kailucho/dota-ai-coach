using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

/// <summary>
/// Confirms the bundled hero_item_popularity.json (docs/adr/0024) loads
/// correctly and that lookups match values independently computed on the
/// Python side (not just a round-trip self-check) -- values below were
/// read directly out of
/// tools/DotaAiCoach.Training/models/item-ranker-v1.0-20260810T203622Z/hero_item_popularity.json
/// via a one-off Python inspection at fixture-generation time, not
/// derived from this C# loader.
/// </summary>
public class HeroItemPopularityTableTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Models", "item-ranker", "hero_item_popularity.json");

    private static HeroItemPopularityTable MakeTable() => HeroItemPopularityTable.Load(FixturePath);

    [Fact]
    public void Loads_the_bundled_table_and_exposes_all_114_items()
    {
        var table = MakeTable();

        table.AllItems.Should().HaveCount(114);
    }

    [Fact]
    public void HeroPopularity_matches_a_known_python_computed_value()
    {
        var table = MakeTable();

        // hero_id=1 (Anti-Mage), item="abyssal_blade" -- independently
        // verified against the Python-side JSON (see class doc comment).
        var value = table.HeroPopularity(1, "abyssal_blade");

        value.Should().BeApproximately(0.04587375182883117, 1e-12);
    }

    [Fact]
    public void GlobalPopularity_matches_a_known_python_computed_value()
    {
        var table = MakeTable();

        var value = table.GlobalPopularity("abyssal_blade");

        value.Should().BeApproximately(0.0038562806335318183, 1e-12);
    }

    [Fact]
    public void HeroMinuteBucketPopularity_matches_a_known_python_computed_value()
    {
        var table = MakeTable();

        var value = table.HeroMinuteBucketPopularity(1, "early", "abyssal_blade");

        value.Should().BeApproximately(0.0011261968534082938, 1e-12);
    }

    [Fact]
    public void HeroPopularity_falls_back_to_global_for_an_unknown_hero()
    {
        var table = MakeTable();

        var globalValue = table.GlobalPopularity("abyssal_blade");
        var fallbackValue = table.HeroPopularity(-999_999, "abyssal_blade");

        fallbackValue.Should().Be(globalValue);
    }

    [Fact]
    public void HeroMinuteBucketPopularity_falls_back_to_global_for_an_unknown_bucket()
    {
        var table = MakeTable();

        var globalValue = table.GlobalPopularity("abyssal_blade");
        var fallbackValue = table.HeroMinuteBucketPopularity(1, "not_a_real_bucket", "abyssal_blade");

        fallbackValue.Should().Be(globalValue);
    }

    [Theory]
    [InlineData(0, "early")]
    [InlineData(14, "early")]
    [InlineData(15, "mid")]
    [InlineData(29, "mid")]
    [InlineData(30, "late")]
    [InlineData(60, "late")]
    public void MinuteBucket_matches_item_ranker_pys_minute_bucket_boundaries(double minute, string expected)
    {
        HeroItemPopularityTable.MinuteBucket(minute).Should().Be(expected);
    }
}
