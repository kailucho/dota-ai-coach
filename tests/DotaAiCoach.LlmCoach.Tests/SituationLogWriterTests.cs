using DotaAiCoach.Domain.LlmCoach;
using DotaAiCoach.LlmCoach;
using FluentAssertions;

namespace DotaAiCoach.LlmCoach.Tests;

public class SituationLogWriterTests
{
    private static ManualSituationInput SampleInput() => new(
        MinuteOfMatch: 15,
        OwnHero: "Pudge",
        OwnKda: "3/1/5",
        EnemyScoreboard: new ScoreboardExtractionResult(
        [
            new EnemyScoreboardEntry("Anti-Mage", 8, new ScoreboardKda(2, 1, 3)),
            new EnemyScoreboardEntry("Invoker", 9, null),
        ]),
        ExtraNotes: null);

    [Fact]
    public async Task AppendAsync_writes_one_JSON_line_per_call()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DotaAiCoach.LlmCoach.Tests", Guid.NewGuid().ToString());
        var writer = new SituationLogWriter(directory);

        var input = SampleInput();
        var advice = new LlmSituationAdvice("Compra BKB", DateTimeOffset.UtcNow, "gpt-5");

        await writer.AppendAsync(input, advice, CancellationToken.None);
        await writer.AppendAsync(input, advice, CancellationToken.None);

        var logFile = Path.Combine(directory, "situations.jsonl");
        var lines = await File.ReadAllLinesAsync(logFile);

        lines.Should().HaveCount(2);
        lines[0].Should().Contain("Pudge").And.Contain("Compra BKB").And.Contain("Anti-Mage");
    }

    [Fact]
    public async Task AppendAsync_never_throws_when_the_log_file_path_is_a_directory()
    {
        // situations.jsonl already exists as a directory, not a file --
        // File.AppendAllTextAsync fails inside AppendAsync's try/catch.
        // Logging must degrade silently, never block showing the LLM's
        // answer to the player.
        var directory = Path.Combine(Path.GetTempPath(), "DotaAiCoach.LlmCoach.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(directory, "situations.jsonl"));
        var writer = new SituationLogWriter(directory);

        var input = SampleInput();
        var advice = new LlmSituationAdvice("test", DateTimeOffset.UtcNow, "gpt-5");

        var act = async () => await writer.AppendAsync(input, advice, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
