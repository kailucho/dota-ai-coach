namespace DotaAiCoach.Domain.MlInference;

/// <summary>
/// Plain input for one (state, candidate item) row scored by the
/// item-ranker model (docs/adr/0022). The three popularity fields are
/// Bayesian-shrunk lookup values that the model's ONNX graph does NOT
/// compute internally — per onnx_export_metadata.json's
/// "popularity_tables_not_exported_note", the popularity tables
/// (tables['global'|'hero'|'hero_minute'] in the training-time
/// model.pkl) are not embedded in the exported graph and have no
/// JSON/Parquet sidecar in the model directory. Rather than fabricate
/// or hardcode approximations of those tables inside MlInference, this
/// predictor pushes the responsibility of supplying the three
/// popularity figures to the caller (expected to be the Feature Engine,
/// which is the layer that already owns "MatchState + reference data ->
/// normalized features" per dota-architecture's layering) — see
/// docs/adr/0023 for the full reasoning.
/// </summary>
public sealed record ItemRankInput
{
    public required double MinuteOfMatch { get; init; }
    public required double ItemsOwnedBeforeCount { get; init; }

    /// <summary>
    /// The match's average rank tier (OpenDota-style: 10s digit = medal,
    /// 1s digit = stars, e.g. 44 = Archon 4) — PublicPreflight per
    /// dota-fairplay, known from the player's own profile before/during
    /// the match (see CoachSession.SetAverageRankTier), never opponent-
    /// derived. Callers without a known value must supply the model's
    /// training-time fallback (see the bundled model's metadata.json ->
    /// data.avg_rank_tier_fill), not an arbitrary default — an unrelated
    /// fallback value would push every prediction off-distribution.
    /// </summary>
    public required double AverageRankTier { get; init; }

    public required double ItemPopularityForHero { get; init; }
    public required double ItemPopularityForHeroMinuteBucket { get; init; }
    public required double ItemGlobalPopularity { get; init; }
}

/// <summary>
/// Raw LambdaMART relevance score (not a probability) — per
/// onnx_export_metadata.json, rank candidates for the same query/state
/// by descending score.
/// </summary>
public sealed record ItemRankOutput
{
    public required double RankScore { get; init; }
}

/// <summary>
/// Domain-defined abstraction over the item-ranker ONNX model.
/// Implemented by DotaAiCoach.MlInference.
/// </summary>
public interface IItemRankPredictor
{
    /// <summary>
    /// The value callers should use for ItemRankInput.AverageRankTier
    /// when the player's actual rank tier is unknown (e.g. GSI/profile
    /// hasn't resolved it yet) — the bundled model's own training-time
    /// fill value, not an arbitrary default.
    /// </summary>
    double AverageRankTierFill { get; }

    ItemRankOutput Predict(ItemRankInput input);
}
