using DotaAiCoach.Domain.LlmCoach;
using DotaAiCoach.LlmCoach;

namespace DotaAiCoach.Host;

public sealed class LlmCoachSession : ILlmCoachSession
{
    private readonly IScoreboardExtractor? _extractor;
    private readonly ISituationAdvisor? _advisor;
    private readonly SituationLogWriter _logWriter = new();

    private LlmCoachSession(IScoreboardExtractor? extractor, ISituationAdvisor? advisor)
    {
        _extractor = extractor;
        _advisor = advisor;
    }

    public static LlmCoachSession Create() =>
        new(OpenAiScoreboardExtractor.TryCreate(), OpenAiSituationAdvisor.TryCreate());

    public bool IsAvailable => _extractor is not null && _advisor is not null;

    public async Task<ScoreboardExtractionResult> ExtractScoreboardAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken ct)
    {
        if (_extractor is null)
        {
            throw new InvalidOperationException(
                "No OPENAI_API_KEY configured. Set the environment variable and restart the app.");
        }

        return await _extractor.ExtractAsync(imageBytes, mediaType, ct);
    }

    public async Task<LlmSituationAdvice> AskForAdviceAsync(ManualSituationInput input, CancellationToken ct)
    {
        if (_advisor is null)
        {
            throw new InvalidOperationException(
                "No OPENAI_API_KEY configured. Set the environment variable and restart the app.");
        }

        var advice = await _advisor.GetAdviceAsync(input, ct);
        await _logWriter.AppendAsync(input, advice, ct);
        return advice;
    }
}
