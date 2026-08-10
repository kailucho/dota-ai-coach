using System.Text.Json;
using System.Text.Json.Serialization;
using DotaAiCoach.Domain.MlInference;

namespace DotaAiCoach.MlInference;

/// <summary>
/// Loads and exposes the item-ranker's Bayesian-shrunk popularity lookup
/// tables (docs/adr/0020 Decision 3, docs/adr/0024) from the bundled
/// <c>Models/item-ranker/hero_item_popularity.json</c> sidecar exported
/// from <c>model.pkl</c>'s <c>tables['global'|'hero'|'hero_minute']</c> by
/// <c>tools/DotaAiCoach.Training/scripts/export_popularity_table.py</c>.
///
/// This class is deliberately separate from <see cref="ItemRankPredictor"/>
/// and does NOT change <c>IItemRankPredictor</c>'s shape — see docs/adr/0024
/// Decision (caller-supplied vs internal loading) for the reasoning: the
/// exported JSON is 2.6MB with ~58,000 total lookup entries, which is
/// non-trivial to eagerly parse into memory on every predictor
/// construction, and a caller (the Feature Engine, per ADR 0023 Decision 5)
/// may want to look up the same popularity value many times across a
/// single live match without re-touching this class at all — keeping the
/// loader separate lets a caller construct it once, cache it, and pass
/// plain doubles into <c>ItemRankInput</c> exactly as before, rather than
/// forcing every <c>ItemRankPredictor</c> construction (including in tests
/// that don't need popularity data at all, e.g. isolated ONNX parity
/// checks) to pay the parse cost.
///
/// Lookup semantics mirror <c>item_ranker.py</c>'s
/// <c>build_candidate_rows</c> nested-fallback behavior exactly: a missing
/// (hero, item) entry in the hero table falls back to the global table;
/// a missing (hero, minute_bucket, item) entry in the hero_minute table
/// falls back to the global table (NOT to the hero table — verified
/// against <c>build_candidate_rows</c>'s actual
/// <c>fillna(item_global_popularity)</c> calls, which both use the global
/// column as the fallback, not a two-stage hero-then-global chain).
/// </summary>
public sealed class HeroItemPopularityTable : IHeroItemPopularityLookup
{
    private readonly Dictionary<string, double> _global;
    private readonly Dictionary<int, Dictionary<string, double>> _hero;
    private readonly Dictionary<int, Dictionary<string, Dictionary<string, double>>> _heroMinute;

    public IReadOnlyList<string> AllItems { get; }

    private HeroItemPopularityTable(
        Dictionary<string, double> global,
        Dictionary<int, Dictionary<string, double>> hero,
        Dictionary<int, Dictionary<string, Dictionary<string, double>>> heroMinute,
        IReadOnlyList<string> allItems)
    {
        _global = global;
        _hero = hero;
        _heroMinute = heroMinute;
        AllItems = allItems;
    }

    public static HeroItemPopularityTable Load(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<PopularityTableDto>(json)
            ?? throw new InvalidOperationException($"Could not parse hero_item_popularity.json at {path}");

        var hero = dto.Hero.ToDictionary(
            kv => int.Parse(kv.Key),
            kv => kv.Value);
        var heroMinute = dto.HeroMinute.ToDictionary(
            kv => int.Parse(kv.Key),
            kv => kv.Value);

        return new HeroItemPopularityTable(dto.Global, hero, heroMinute, dto.AllItems);
    }

    public static HeroItemPopularityTable LoadFromBundledModels() =>
        Load(ModelPathResolver.ResolvePath("item-ranker", "hero_item_popularity.json"));

    /// <summary>Unconditional (train-split) popularity for an item; 0.0 if never observed.</summary>
    public double GlobalPopularity(string itemName) =>
        _global.TryGetValue(itemName, out var v) ? v : 0.0;

    /// <summary>
    /// Per-hero, Bayesian-shrunk popularity. Falls back to global
    /// popularity if this hero has no entry at all (matches
    /// build_candidate_rows' fillna(item_global_popularity)).
    /// </summary>
    public double HeroPopularity(int heroId, string itemName)
    {
        if (_hero.TryGetValue(heroId, out var items) && items.TryGetValue(itemName, out var v))
        {
            return v;
        }
        return GlobalPopularity(itemName);
    }

    /// <summary>
    /// Per-(hero, minute bucket), nested-shrunk popularity. minuteBucket
    /// must be "early" (&lt;15min), "mid" (15-30min), or "late" (&gt;=30min),
    /// matching item_ranker.py's _minute_bucket exactly. Falls back to
    /// global popularity (not hero popularity) if missing, matching
    /// build_candidate_rows' actual fillna target for this column.
    /// </summary>
    public double HeroMinuteBucketPopularity(int heroId, string minuteBucket, string itemName)
    {
        if (_heroMinute.TryGetValue(heroId, out var buckets)
            && buckets.TryGetValue(minuteBucket, out var items)
            && items.TryGetValue(itemName, out var v))
        {
            return v;
        }
        return GlobalPopularity(itemName);
    }

    /// <summary>Minute (in-game clock, always LIVE_ALLOWED) -> bucket name, matching item_ranker.py's _minute_bucket.</summary>
    public static string MinuteBucket(double minute) =>
        minute < 15 ? "early" : minute < 30 ? "mid" : "late";

    private sealed class PopularityTableDto
    {
        [JsonPropertyName("all_items")]
        public required List<string> AllItems { get; init; }

        [JsonPropertyName("global")]
        public required Dictionary<string, double> Global { get; init; }

        [JsonPropertyName("hero")]
        public required Dictionary<string, Dictionary<string, double>> Hero { get; init; }

        [JsonPropertyName("hero_minute")]
        public required Dictionary<string, Dictionary<string, Dictionary<string, double>>> HeroMinute { get; init; }
    }
}
