namespace DotaAiCoach.PlayerProfile.Scoring;

/// <summary>Tunable constants for HeroComfortScorer — see docs/adr/0015 for the derivation.</summary>
public sealed class HeroComfortOptions
{
    /// <summary>Pseudo-count strength of the player's own overall win rate when shrinking toward 0.5.</summary>
    public double PriorStrengthOverall { get; set; } = 50.0;

    /// <summary>Pseudo-count strength of the per-hero Beta prior derived from the player's overall win rate.</summary>
    public double PriorStrength { get; set; } = 20.0;

    /// <summary>Z-score for the Wilson lower confidence bound (1.28 ~= 80% one-sided).</summary>
    public double ConfidenceZ { get; set; } = 1.28;

    /// <summary>Games at which VolumeFactor saturates to 1.0.</summary>
    public int VolumeSaturationGames { get; set; } = 40;

    /// <summary>Half-life, in days, of RecencyFactor's exponential decay.</summary>
    public double RecencyHalfLifeDays { get; set; } = 60.0;

    /// <summary>Minimum matches carrying GPM/XPM data required to compute PerformanceFactor/Consistency.</summary>
    public int MinPerformanceSamples { get; set; } = 3;

    /// <summary>Window, in days, used to compute PatchFamiliarity as a recency proxy (OpenDota match summaries carry no patch id).</summary>
    public int PatchWindowDays { get; set; } = 45;

    /// <summary>Spread divisor for the Consistency component's z-score normalization.</summary>
    public double ConsistencySpread { get; set; } = 2.0;

    /// <summary>Minimum matches carrying lane_role required to compute RoleFamiliarity.</summary>
    public int MinRoleFamiliaritySamples { get; set; } = 5;

    // Combination weights. Renormalized over available (non-null) components.
    public double WeightAdjustedWinRate { get; set; } = 0.30;
    public double WeightVolume { get; set; } = 0.20;
    public double WeightRecency { get; set; } = 0.15;
    public double WeightPerformance { get; set; } = 0.15;
    public double WeightConsistency { get; set; } = 0.08;
    public double WeightPatchFamiliarity { get; set; } = 0.07;
    public double WeightRoleFamiliarity { get; set; } = 0.05;
}
