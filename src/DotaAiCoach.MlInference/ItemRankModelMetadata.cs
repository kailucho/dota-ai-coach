using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotaAiCoach.MlInference;

/// <summary>
/// Minimal deserialization shape for the subset of the item-ranker's
/// training-time metadata.json this project consumes at runtime — just
/// data.avg_rank_tier_fill, the TRAIN split's median avg_rank_tier used
/// as the missing-value fill during training (item_ranker.py's
/// train_and_evaluate). Callers of ItemRankPredictor without a known
/// AverageRankTier must use this same fill value, not an arbitrary
/// default — an unrelated fallback would push predictions off the
/// distribution the model was actually trained on. Reads the bundled
/// file rather than hand-copying the number, so it can never silently
/// drift from what the model was actually trained with.
/// </summary>
internal sealed class ItemRankModelMetadata
{
    [JsonPropertyName("data")]
    public required ItemRankModelData Data { get; init; }

    public static ItemRankModelMetadata Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ItemRankModelMetadata>(json)
            ?? throw new InvalidOperationException($"Could not parse metadata.json at {path}");
    }
}

internal sealed class ItemRankModelData
{
    [JsonPropertyName("avg_rank_tier_fill")]
    public required double AverageRankTierFill { get; init; }
}
