namespace DotaAiCoach.Domain.MlInference;

/// <summary>
/// Plain, infrastructure-free input for the LIVE_ALLOWED win-probability
/// model (see docs/adr/0022). Field order here does not need to match the
/// model's tensor order — the MlInference implementation is responsible
/// for mapping these named fields into the model's documented feature
/// order; Domain callers never need to know that order.
/// All fields are LIVE_ALLOWED or PUBLIC_PREFLIGHT per features_spec.yaml
/// (docs/adr/0022, Decision 2) — nothing POSTGAME_ONLY may be added here
/// without a corresponding FairPlay re-classification.
/// </summary>
public sealed record WinProbabilityInput
{
    public required double GoldAdvantageAtMinute { get; init; }
    public required double XpAdvantageAtMinute { get; init; }
    public required double GoldAdvantageSlope5 { get; init; }
    public required double XpAdvantageSlope5 { get; init; }
    public required double KillsDifferenceAtMinute { get; init; }
    public required double DeathsInLast5MinDifference { get; init; }
    public required double TowersKilledDifference { get; init; }
    public required double BarracksKilledDifference { get; init; }
    public required double RoshanKilledDifference { get; init; }
    public required double HeroPickWinrateDifference { get; init; }
    public required double AverageRankTier { get; init; }
    public required double MinuteOfMatch { get; init; }

    /// <summary>
    /// true/false when known, null when not yet observed (encoded as 0.5
    /// by the predictor implementation, matching training-time treatment
    /// of missing first_blood_radiant — see onnx_export_metadata.json).
    /// </summary>
    public bool? FirstBloodRadiant { get; init; }
}

/// <summary>
/// Calibrated (and raw, for debugging) win probability for the Radiant
/// side, produced by the LIVE_ALLOWED win-probability model.
/// </summary>
public sealed record WinProbabilityOutput
{
    public required double RawRadiantWinProbability { get; init; }
    public required double CalibratedRadiantWinProbability { get; init; }
}

/// <summary>
/// Domain-defined abstraction over the win-probability ONNX model.
/// Implemented by DotaAiCoach.MlInference; consumed by
/// DotaAiCoach.RecommendationEngines without any ONNX Runtime type ever
/// crossing that boundary (dota-architecture dependency rule 4/graph).
/// </summary>
public interface IWinProbabilityPredictor
{
    WinProbabilityOutput Predict(WinProbabilityInput input);
}
