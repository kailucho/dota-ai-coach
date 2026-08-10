namespace DotaAiCoach.Domain.MlInference;

/// <summary>
/// Domain-facing abstraction over the item-ranker's Bayesian-shrunk
/// popularity lookup tables (docs/adr/0020/0024), so
/// DotaAiCoach.RecommendationEngines can look up popularity figures to
/// feed ItemRankInput without referencing DotaAiCoach.MlInference
/// directly (dota-architecture dependency rule 4/graph). Implemented by
/// DotaAiCoach.MlInference.HeroItemPopularityTable.
/// </summary>
public interface IHeroItemPopularityLookup
{
    double GlobalPopularity(string itemName);
    double HeroPopularity(int heroId, string itemName);
    double HeroMinuteBucketPopularity(int heroId, string minuteBucket, string itemName);
}
