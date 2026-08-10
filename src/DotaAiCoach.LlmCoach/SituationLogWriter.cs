using System.Text.Json;
using DotaAiCoach.Domain.LlmCoach;

namespace DotaAiCoach.LlmCoach;

/// <summary>
/// Appends every (ManualSituationInput, LlmSituationAdvice) pair to a
/// JSONL file under %LocalAppData%\DotaAiCoach\llm-situations\, one line
/// per call. This is the durable dataset the user wants to eventually
/// use to refine the trained Item Ranker model — no automatic training
/// pipeline consumes it yet; it just needs to exist and grow every time
/// the manual "ask for a build" panel is used. Best-effort: a logging
/// failure must never block showing the LLM's answer to the player.
/// </summary>
public sealed class SituationLogWriter
{
    private readonly string _logFilePath;

    public SituationLogWriter() : this(overrideDirectory: null)
    {
    }

    internal SituationLogWriter(string? overrideDirectory)
    {
        var directory = overrideDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach",
            "llm-situations");

        Directory.CreateDirectory(directory);
        _logFilePath = Path.Combine(directory, "situations.jsonl");
    }

    public async Task AppendAsync(ManualSituationInput input, LlmSituationAdvice advice, CancellationToken ct)
    {
        try
        {
            var entry = new LoggedSituation(input, advice);
            var line = JsonSerializer.Serialize(entry);
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, ct);
        }
        catch
        {
            // Logging is a nice-to-have for future training data, never a
            // reason to fail the user-facing recommendation.
        }
    }

    private sealed record LoggedSituation(ManualSituationInput Input, LlmSituationAdvice Advice);
}
