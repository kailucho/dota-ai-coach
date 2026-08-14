namespace DotaAiCoach.Domain.LlmCoach;

/// <summary>
/// One enemy hero's stats as read off the player's own scoreboard
/// screenshot by a vision LLM. fairPlayClassification: VISIBLE_SCREEN —
/// the image is the player's own capture of their own client, same basis
/// as the deferred Computer Vision phase (see docs/ROADMAP.md's Fase 9
/// note and dota-fairplay). Level/Kda are nullable because the model is
/// instructed to omit a field it could not read clearly, rather than
/// guess.
/// </summary>
public sealed record EnemyScoreboardEntry(
    string Hero,
    int? Level,
    ScoreboardKda? Kda);

public sealed record ScoreboardKda(int Kills, int Deaths, int Assists);

/// <summary>
/// The full result of extracting a scoreboard screenshot: every enemy
/// hero legible in the image, reported as-is (no "expected enemies"
/// hint is given or needed — the extractor just reads what's there). The
/// player's own KDA is NOT part of this — it's already known live from
/// GSI (see ManualSituationInput.OwnKda), not something the screenshot
/// needs to supply.
/// </summary>
public sealed record ScoreboardExtractionResult(IReadOnlyList<EnemyScoreboardEntry> Enemies);
