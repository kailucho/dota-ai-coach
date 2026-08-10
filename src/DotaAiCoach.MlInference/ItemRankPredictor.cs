using DotaAiCoach.Domain.MlInference;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DotaAiCoach.MlInference;

/// <summary>
/// ONNX Runtime-backed implementation of IItemRankPredictor, for the
/// item-ranker LightGBM model (docs/adr/0022). Feature order copied
/// verbatim from Models/item-ranker/onnx_export_metadata.json's
/// input_schema.feature_order. No calibration step (ranking model, raw
/// LambdaMART score). The popularity-table lookups are supplied by the
/// caller — see ItemRankInput's doc comment and docs/adr/0023 for why.
/// </summary>
public sealed class ItemRankPredictor : IItemRankPredictor, IDisposable
{
    private const string ModelFolder = "item-ranker";
    private readonly InferenceSession _session;

    /// <summary>
    /// The value callers should pass as ItemRankInput.AverageRankTier
    /// when the player's actual rank tier is unknown — the TRAIN split's
    /// median avg_rank_tier at training time (see the bundled
    /// metadata.json's data.avg_rank_tier_fill), read directly from the
    /// bundled model rather than hardcoded so it can never drift from
    /// what this specific model was actually trained with.
    /// </summary>
    public double AverageRankTierFill { get; }

    public ItemRankPredictor()
        : this(ModelPathResolver.ResolvePath(ModelFolder, "model.onnx"), ModelPathResolver.ResolvePath(ModelFolder, "metadata.json"))
    {
    }

    internal ItemRankPredictor(string onnxModelPath, string metadataPath)
    {
        _session = new InferenceSession(onnxModelPath);
        AverageRankTierFill = ItemRankModelMetadata.Load(metadataPath).Data.AverageRankTierFill;
    }

    public ItemRankOutput Predict(ItemRankInput input)
    {
        // Exact order per onnx_export_metadata.json's input_schema.feature_order:
        // minute, n_items_owned_before, avg_rank_tier, item_popularity_for_hero,
        // item_popularity_for_hero_minute_bucket, item_global_popularity.
        var features = new float[]
        {
            (float)input.MinuteOfMatch,
            (float)input.ItemsOwnedBeforeCount,
            (float)input.AverageRankTier,
            (float)input.ItemPopularityForHero,
            (float)input.ItemPopularityForHeroMinuteBucket,
            (float)input.ItemGlobalPopularity,
        };

        var tensor = new DenseTensor<float>(features, [1, features.Length]);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) };

        using var results = _session.Run(inputs);
        var score = results.First().AsEnumerable<float>().First();

        return new ItemRankOutput { RankScore = score };
    }

    public void Dispose() => _session.Dispose();
}
