namespace DotaAiCoach.Domain.LlmCoach;

/// <summary>
/// What feeds the manual "ask for a build" panel: the player's own hero/
/// KDA (typed — trivially known, no need to screenshot it) plus the
/// enemy team's scoreboard state, extracted from a screenshot the player
/// captured of their own scoreboard (fairPlayClassification:
/// VISIBLE_SCREEN — same basis as the deferred Computer Vision phase,
/// see docs/ROADMAP.md's Fase 9 note and dota-fairplay). EnemyScoreboard
/// is never derived from GSI/CV automation — it's the output of a
/// player-initiated, player-captured image.
/// </summary>
public sealed record ManualSituationInput(
    int MinuteOfMatch,
    string OwnHero,
    string OwnKda,
    ScoreboardExtractionResult EnemyScoreboard,
    string? ExtraNotes);

/// <summary>
/// The LLM's build recommendation for one ManualSituationInput, plus the
/// raw request/response pair needed to later use this as training data
/// for the Item Ranker (see docs/ROADMAP.md's LLM-refinement note). Never
/// consumed by the live rule engine directly — this is a separate,
/// user-triggered surface (see dota-live-coach: no live message-budget
/// gating applies here since it's not an automatic push).
/// </summary>
public sealed record LlmSituationAdvice(
    string ResponseText,
    DateTimeOffset GeneratedAtUtc,
    string ModelId);
