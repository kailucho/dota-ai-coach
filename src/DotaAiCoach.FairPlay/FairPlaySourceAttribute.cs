namespace DotaAiCoach.FairPlay;

/// <summary>
/// Marks a type as carrying data from a classified source, and requires the
/// project's mandatory five-field FairPlay documentation (source,
/// availability, visibility, fairPlayClassification, justification) to be
/// filled in at the declaration site.
///
/// Applied at the infrastructure boundary — the point where raw observed
/// data first becomes a typed Domain object — and asserted mechanically by
/// DotaAiCoach.FairPlay.Tests, per dota-fairplay's testing requirement that
/// classification be enforced structurally, not just documented.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum,
    Inherited = false,
    AllowMultiple = false)]
public sealed class FairPlaySourceAttribute : Attribute
{
    public FairPlaySourceAttribute(DataSourceKind source, FairPlayClassification classification)
    {
        Source = source;
        Classification = classification;
    }

    /// <summary>Where the data physically comes from.</summary>
    public DataSourceKind Source { get; }

    /// <summary>One of the six FairPlay categories.</summary>
    public FairPlayClassification Classification { get; }

    /// <summary>When this data exists / is populated.</summary>
    public string? Availability { get; init; }

    /// <summary>What a legitimate player can concretely see that corresponds
    /// to this data — must cite an actual screen element or GSI field name,
    /// per dota-fairplay's documentation template.</summary>
    public string? Visibility { get; init; }

    /// <summary>Why this classification is correct for this specific data point.</summary>
    public string? Justification { get; init; }
}
