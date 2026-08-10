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
/// The full result of extracting a scoreboard screenshot: entries for
/// whichever of the requested enemy heroes were actually legible in the
/// image. Never includes heroes outside the requested list — the
/// extractor is instructed to restrict output to it, mirroring
/// dota-plus-free's tickExtract.ts normalization.
/// </summary>
public sealed record ScoreboardExtractionResult(IReadOnlyList<EnemyScoreboardEntry> Enemies);
