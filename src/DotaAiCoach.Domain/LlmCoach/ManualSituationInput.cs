using DotaAiCoach.Domain.Recommendations;

namespace DotaAiCoach.Domain.LlmCoach;

/// <summary>
/// What feeds the manual "ask for a build" panel. MinuteOfMatch, OwnHero,
/// OwnKda, Gold, OwnItems, and HeroLevel are all auto-filled from the same
/// live GSI-derived state already driving the overlay's ItemPlan
/// (fairPlayClassification: LIVE_ALLOWED — the player's own state, per
/// dota-fairplay) — never typed by the player. EnemyScoreboard comes
/// entirely from a screenshot the player captured of their own scoreboard
/// (fairPlayClassification: VISIBLE_SCREEN — same basis as the deferred
/// Computer Vision phase, see docs/ROADMAP.md's Fase 9 note). RankTier is
/// PUBLIC_PREFLIGHT, sourced from OpenDota via ProfileSession — the same
/// value already fed into ICoachSession.SetAverageRankTier, not a new data
/// source. GamePatch is likewise PUBLIC_PREFLIGHT, fetched once from
/// OpenDota's /constants/patch and cached — GSI does not expose a
/// human-readable patch number, only an internal client build number.
/// </summary>
public sealed record ManualSituationInput(
    int MinuteOfMatch,
    string OwnHero,
    ScoreboardKda? OwnKda,
    ScoreboardExtractionResult EnemyScoreboard,
    string? ExtraNotes,
    int? Gold = null,
    IReadOnlyList<string>? OwnItems = null,
    int? HeroLevel = null,
    int? RankTier = null,
    string? GamePatch = null);

/// <summary>
/// The LLM's build recommendation for one ManualSituationInput, in the
/// same ItemPlan shape the ML-driven overlay panel uses -- so the LLM's
/// answer can replace that panel's contents directly instead of being
/// shown as separate free-form text (see docs/ROADMAP.md's LLM-refinement
/// note for why this doubles as future Item Ranker training data). Never
/// consumed by the live rule engine directly — this is a separate,
/// user-triggered surface (see dota-live-coach: no live message-budget
/// gating applies here since it's not an automatic push).
/// </summary>
public sealed record LlmSituationAdvice(
    ItemPlan ItemPlan,
    DateTimeOffset GeneratedAtUtc,
    string ModelId);
