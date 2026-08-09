namespace DotaAiCoach.Domain.Match;

/// <summary>
/// The canonical, current, normalized game state — the single source of
/// truth everything downstream of GSI ingestion consumes. Built and
/// mutated in place by DotaAiCoach.Gsi's normalizer via field-wise merge
/// of partial payloads (see dota-gsi's state-diffing guidance); never
/// replaced wholesale, since a GSI payload only ever contains the sections
/// that changed.
///
/// Setters are internal: only the Gsi assembly (via InternalsVisibleTo, or
/// by construction through the normalizer) is expected to mutate this —
/// everything else treats it as read-only current state.
/// </summary>
public sealed class MatchState
{
    public MatchSnapshot Match { get; internal set; } = MatchSnapshot.Empty;
    public PlayerSnapshot Player { get; internal set; } = PlayerSnapshot.Empty;
    public HeroSnapshot Hero { get; internal set; } = HeroSnapshot.Empty;
    public ItemsSnapshot Items { get; internal set; } = ItemsSnapshot.Empty;
    public AbilitiesSnapshot Abilities { get; internal set; } = AbilitiesSnapshot.Empty;

    public DateTimeOffset? LastUpdatedAt { get; internal set; }
    public int UpdateCount { get; internal set; }

    /// <summary>Set true after a detected feed gap (see dota-gsi's reconnection
    /// guidance); left for the consumer that acts on it to observe, never
    /// auto-cleared by the normalizer.</summary>
    public bool HasStateDiscontinuity { get; internal set; }

    /// <summary>Shallow copy — snapshots are immutable records, so copying the
    /// top-level references is sufficient to isolate a caller's view from
    /// subsequent mutation of this instance.</summary>
    public MatchState Clone() => new()
    {
        Match = Match,
        Player = Player,
        Hero = Hero,
        Items = Items,
        Abilities = Abilities,
        LastUpdatedAt = LastUpdatedAt,
        UpdateCount = UpdateCount,
        HasStateDiscontinuity = HasStateDiscontinuity
    };
}
