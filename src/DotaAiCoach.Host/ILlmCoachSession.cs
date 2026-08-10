using DotaAiCoach.Domain.LlmCoach;

namespace DotaAiCoach.Host;

/// <summary>
/// The only surface DotaAiCoach.UiOverlay is allowed to depend on for the
/// manual "ask for a build" LLM feature — mirrors ICoachSession/
/// IProfileSession's firewall so UiOverlay never references
/// DotaAiCoach.LlmCoach (OpenAI SDK types) directly.
/// </summary>
public interface ILlmCoachSession
{
    /// <summary>
    /// False when no OPENAI_API_KEY is configured — UiOverlay uses this
    /// to disable the panel with an explanatory message instead of
    /// erroring on first use.
    /// </summary>
    bool IsAvailable { get; }

    Task<ScoreboardExtractionResult> ExtractScoreboardAsync(
        byte[] imageBytes,
        string mediaType,
        IReadOnlyList<string> expectedEnemyHeroes,
        CancellationToken ct);

    Task<LlmSituationAdvice> AskForAdviceAsync(ManualSituationInput input, CancellationToken ct);
}
