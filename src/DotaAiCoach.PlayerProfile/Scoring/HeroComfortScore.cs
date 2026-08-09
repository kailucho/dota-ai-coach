namespace DotaAiCoach.PlayerProfile.Scoring;

public sealed record HeroComfortScore
{
    public required int HeroId { get; init; }

    /// <summary>0-100, or null when the profile is unavailable or the player has zero games on this hero — never a fabricated 0 or 50.</summary>
    public double? Score { get; init; }

    /// <summary>0-1, reported separately from Score, never folded into it.</summary>
    public double? Confidence { get; init; }

    public ComfortComponents Components { get; init; } = ComfortComponents.Empty;
    public string? PrimaryReason { get; init; }

    public static readonly HeroComfortScore Empty = new() { HeroId = 0 };
}
