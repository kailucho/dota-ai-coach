namespace DotaAiCoach.PlayerProfile.Dna;

public sealed record DnaDimensionScore
{
    public required DotaDnaDimension Dimension { get; init; }

    /// <summary>0-100, or null when Availability != Computed.</summary>
    public double? Score { get; init; }

    public required DnaAvailability Availability { get; init; }
    public string? Explanation { get; init; }
    public int? SampleSize { get; init; }
}
