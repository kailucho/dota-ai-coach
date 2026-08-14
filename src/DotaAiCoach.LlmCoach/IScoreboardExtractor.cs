using DotaAiCoach.Domain.LlmCoach;

namespace DotaAiCoach.LlmCoach;

public interface IScoreboardExtractor
{
    /// <param name="imageBytes">Raw bytes of the player's own scoreboard screenshot.</param>
    /// <param name="mediaType">e.g. "image/png" or "image/jpeg".</param>
    Task<ScoreboardExtractionResult> ExtractAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken ct);
}
