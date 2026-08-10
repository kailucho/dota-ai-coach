using DotaAiCoach.Domain.LlmCoach;

namespace DotaAiCoach.LlmCoach;

public interface IScoreboardExtractor
{
    /// <param name="imageBytes">Raw bytes of the player's own scoreboard screenshot.</param>
    /// <param name="mediaType">e.g. "image/png" or "image/jpeg".</param>
    /// <param name="expectedEnemyHeroes">
    /// The 5 enemy hero names as known from the current match (e.g. GSI
    /// top-hero-bar, if available, or manually typed) — restricts and
    /// normalizes the extraction, mirroring dota-plus-free's tickExtract
    /// "enemies" hint.
    /// </param>
    Task<ScoreboardExtractionResult> ExtractAsync(
        byte[] imageBytes,
        string mediaType,
        IReadOnlyList<string> expectedEnemyHeroes,
        CancellationToken ct);
}
