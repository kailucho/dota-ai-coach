using DotaAiCoach.Domain.LlmCoach;

namespace DotaAiCoach.LlmCoach;

public interface ISituationAdvisor
{
    Task<LlmSituationAdvice> GetAdviceAsync(ManualSituationInput input, CancellationToken ct);
}
