namespace DotaAiCoach.PlayerProfile.Dna;

/// <summary>Always carries all eleven dimensions (see DotaDnaDimension) — deferred ones report a truthful DnaAvailability, never a fabricated score.</summary>
public sealed record DotaDnaProfile
{
    public IReadOnlyList<DnaDimensionScore> Dimensions { get; init; } = Array.Empty<DnaDimensionScore>();

    /// <summary>Highest-scoring computed dimensions; empty unless at least two dimensions are Computed.</summary>
    public IReadOnlyList<DotaDnaDimension> PrimaryStrengths { get; init; } = Array.Empty<DotaDnaDimension>();

    /// <summary>Lowest-scoring computed dimensions; empty unless at least two dimensions are Computed.</summary>
    public IReadOnlyList<DotaDnaDimension> PrimaryWeaknesses { get; init; } = Array.Empty<DotaDnaDimension>();

    public static readonly DotaDnaProfile Empty = new();
}
