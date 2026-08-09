namespace DotaAiCoach.PlayerProfile.Scoring;

/// <summary>
/// The individual sub-scores behind a HeroComfortScore, each in [0,1] or
/// null when undecidable, so the score is explainable rather than opaque.
/// </summary>
public sealed record ComfortComponents
{
    public double? AdjustedWinRate { get; init; }
    public double? VolumeFactor { get; init; }
    public double? RecencyFactor { get; init; }
    public double? PerformanceFactor { get; init; }
    public double? RoleFamiliarity { get; init; }
    public double? PatchFamiliarity { get; init; }

    /// <summary>Not yet computed per-hero in this phase — see docs/adr/0015. Reserved for a future phase.</summary>
    public double? MatchupFamiliarity { get; init; }

    public double? Consistency { get; init; }
    public double? ComplexityPenalty { get; init; }

    public static readonly ComfortComponents Empty = new();
}
