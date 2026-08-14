using DotaAiCoach.Domain.LlmCoach;
using DotaAiCoach.LlmCoach;
using FluentAssertions;

namespace DotaAiCoach.LlmCoach.Tests;

public class OpenAiScoreboardExtractorTests
{
    [Fact]
    public void ParseResult_parses_level_and_kda_for_each_enemy()
    {
        var json = """
            {
              "enemy_status": [
                { "hero": "Anti-Mage", "level": 8, "kda": { "k": 2, "d": 1, "a": 3 } },
                { "hero": "Invoker", "level": null, "kda": null }
              ]
            }
            """;

        var result = OpenAiScoreboardExtractor.ParseResult(json);

        result.Enemies.Should().HaveCount(2);
        result.Enemies[0].Hero.Should().Be("Anti-Mage");
        result.Enemies[0].Level.Should().Be(8);
        result.Enemies[0].Kda.Should().Be(new ScoreboardKda(2, 1, 3));
        result.Enemies[1].Hero.Should().Be("Invoker");
        result.Enemies[1].Level.Should().BeNull();
        result.Enemies[1].Kda.Should().BeNull();
    }

    [Fact]
    public void ParseResult_reports_every_hero_the_model_returns_with_no_filtering()
    {
        // No "expected enemies" hint exists anymore -- the extractor
        // reports whatever the model reads, as-is.
        var json = """
            {
              "enemy_status": [
                { "hero": "Anti-Mage", "level": 8, "kda": null },
                { "hero": "Some Other Hero", "level": 5, "kda": null }
              ]
            }
            """;

        var result = OpenAiScoreboardExtractor.ParseResult(json);

        result.Enemies.Should().HaveCount(2);
        result.Enemies.Select(e => e.Hero).Should().BeEquivalentTo(["Anti-Mage", "Some Other Hero"]);
    }

    [Fact]
    public void ParseResult_skips_entries_with_a_blank_hero_name()
    {
        var json = """
            {
              "enemy_status": [
                { "hero": "", "level": 8, "kda": null },
                { "hero": "Anti-Mage", "level": 8, "kda": null }
              ]
            }
            """;

        var result = OpenAiScoreboardExtractor.ParseResult(json);

        result.Enemies.Should().ContainSingle();
        result.Enemies[0].Hero.Should().Be("Anti-Mage");
    }

    [Fact]
    public void ParseResult_returns_an_empty_list_when_no_enemies_are_detected()
    {
        var json = """{ "enemy_status": [] }""";

        var result = OpenAiScoreboardExtractor.ParseResult(json);

        result.Enemies.Should().BeEmpty();
    }
}
