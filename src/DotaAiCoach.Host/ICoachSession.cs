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

    /// <summary>
    /// The always-visible early/mid/late item plan (see ItemPlan) —
    /// deliberately separate from ActiveMessages/LiveMessageBudget's
    /// cooldown-gated feed, per dota-live-coach: a multi-item plan is not
    /// a short live-action message, but the user explicitly asked to see
    /// the full plan persistently rather than one phase at a time. Empty
    /// when there is nothing to plan (e.g. feed not usable).
    /// </summary>
    ItemPlan CurrentItemPlan { get; }

    event EventHandler<CoachUpdate>? Updated;

    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Feeds the player's average rank tier (PUBLIC_PREFLIGHT, sourced
    /// from the separate ProfileSession/OpenDota, never from GSI) into
    /// the win-probability rule's AverageRankTier input. Null clears it
    /// back to the model's documented missing-value fill (0.0). Safe to
    /// call at any time, including before the GSI feed connects — the
    /// next Evaluate() picks it up. See docs/ROADMAP.md.
    /// </summary>
    void SetAverageRankTier(int? rankTier);
}

public sealed record CoachUpdate(
    MatchState State,
    FeedHealth Health,
    IReadOnlyList<DomainEvent> Events,
    IReadOnlyList<LiveMessage> Messages,
    ItemPlan ItemPlan);
