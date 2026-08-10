namespace DotaAiCoach.Domain.MlInference;

/// <summary>
/// Plain input for the draft-baseline model (docs/adr/0022). Both
/// features are PUBLIC_PREFLIGHT-eligible — draft is visible to both
/// teams before the match starts.
/// </summary>
public sealed record DraftScoreInput
{
    public required double HeroWinrateDifference { get; init; }
    public required double MatchupAdvantage { get; init; }
}

/// <summary>
/// Calibrated (and raw, for debugging) win probability for the Radiant
/// side implied by the draft alone, produced by the draft-baseline model.
/// Note (per docs/adr/0022): this model's test AUC (~0.51) is barely
/// above chance — callers should not treat its output as a strong signal
/// without the richer draft feature work docs/adr/0019/0021 already
/// flagged as needed.
/// </summary>
public sealed record DraftScoreOutput
{
    public required double RawRadiantWinProbability { get; init; }
    public required double CalibratedRadiantWinProbability { get; init; }
}

/// <summary>
/// Domain-defined abstraction over the draft-baseline ONNX model.
/// Implemented by DotaAiCoach.MlInference.
/// </summary>
public interface IDraftScorePredictor
{
    DraftScoreOutput Predict(DraftScoreInput input);
}
