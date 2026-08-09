using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Domain.Recommendations;

namespace DotaAiCoach.Host;

/// <summary>
/// The only surface DotaAiCoach.UiOverlay is allowed to depend on. Exposes
/// exclusively Domain types — no GSI wire types, no infrastructure
/// details — so the UI structurally cannot see or depend on how state is
/// actually ingested, per dota-architecture's "UI must not call OpenDota
/// directly / must not see GSI wire types" rule generalized to this
/// slice's GSI ingestion. DotaAiCoach.Architecture.Tests asserts UiOverlay
/// has no reference edge to DotaAiCoach.Gsi at all.
/// </summary>
public interface ICoachSession
{
    MatchState CurrentState { get; }
    FeedHealth Health { get; }
    IReadOnlyList<LiveMessage> ActiveMessages { get; }

    event EventHandler<CoachUpdate>? Updated;

    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}

public sealed record CoachUpdate(
    MatchState State,
    FeedHealth Health,
    IReadOnlyList<DomainEvent> Events,
    IReadOnlyList<LiveMessage> Messages);
