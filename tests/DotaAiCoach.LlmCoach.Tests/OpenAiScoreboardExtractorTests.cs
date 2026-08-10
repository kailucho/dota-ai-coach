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

        var result = OpenAiScoreboardExtractor.ParseResult(json, ["Anti-Mage", "Invoker"]);

        result.Enemies.Should().HaveCount(2);
        result.Enemies[0].Hero.Should().Be("Anti-Mage");
        result.Enemies[0].Level.Should().Be(8);
        result.Enemies[0].Kda.Should().Be(new ScoreboardKda(2, 1, 3));
        result.Enemies[1].Hero.Should().Be("Invoker");
        result.Enemies[1].Level.Should().BeNull();
        result.Enemies[1].Kda.Should().BeNull();
    }

    [Fact]
    public void ParseResult_drops_heroes_not_on_the_expected_list()
    {
        var json = """
            {
              "enemy_status": [
                { "hero": "Anti-Mage", "level": 8, "kda": null },
                { "hero": "Some Unexpected Hero", "level": 5, "kda": null }
              ]
            }
            """;

        var result = OpenAiScoreboardExtractor.ParseResult(json, ["Anti-Mage"]);

        result.Enemies.Should().ContainSingle();
        result.Enemies[0].Hero.Should().Be("Anti-Mage");
    }

    [Fact]
    public void ParseResult_returns_raw_hero_name_when_no_expected_list_was_given()
    {
        var json = """
            {
              "enemy_status": [
                { "hero": "Anti-Mage", "level": 8, "kda": null }
              ]
            }
            """;

        var result = OpenAiScoreboardExtractor.ParseResult(json, []);

        result.Enemies.Should().ContainSingle();
        result.Enemies[0].Hero.Should().Be("Anti-Mage");
    }

    [Theory]
    [InlineData("anti-mage", "Anti-Mage")]
    [InlineData("ANTI-MAGE", "Anti-Mage")]
    [InlineData("Anti-Mage (enemy)", "Anti-Mage")]
    public void NormalizeHero_matches_case_insensitively_and_by_substring(string raw, string expected)
    {
        var normalized = OpenAiScoreboardExtractor.NormalizeHero(raw, [expected]);

        normalized.Should().Be(expected);
    }

    [Fact]
    public void NormalizeHero_returns_null_when_nothing_matches()
    {
        var normalized = OpenAiScoreboardExtractor.NormalizeHero("Completely Different Hero", ["Anti-Mage", "Invoker"]);

        normalized.Should().BeNull();
    }
}
