using DotaAiCoach.Domain.Match;
using FluentAssertions;

namespace DotaAiCoach.Domain.Tests;

public class GameStateParserTests
{
    [Theory]
    [InlineData("DOTA_GAMERULES_STATE_INIT", GameState.Init)]
    [InlineData("DOTA_GAMERULES_STATE_WAIT_FOR_PLAYERS_TO_LOAD", GameState.WaitForPlayersToLoad)]
    [InlineData("DOTA_GAMERULES_STATE_HERO_SELECTION", GameState.HeroSelection)]
    [InlineData("DOTA_GAMERULES_STATE_STRATEGY_TIME", GameState.StrategyTime)]
    [InlineData("DOTA_GAMERULES_STATE_TEAM_SHOWCASE", GameState.TeamShowcase)]
    [InlineData("DOTA_GAMERULES_STATE_WAIT_FOR_MAP_TO_LOAD", GameState.WaitForMapToLoad)]
    [InlineData("DOTA_GAMERULES_STATE_PRE_GAME", GameState.PreGame)]
    [InlineData("DOTA_GAMERULES_STATE_GAME_IN_PROGRESS", GameState.GameInProgress)]
    [InlineData("DOTA_GAMERULES_STATE_POST_GAME", GameState.PostGame)]
    [InlineData("DOTA_GAMERULES_STATE_DISCONNECT", GameState.Disconnect)]
    public void Parse_recognizes_all_known_valve_strings(string raw, GameState expected)
    {
        GameStateParser.Parse(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DOTA_GAMERULES_STATE_SOME_FUTURE_STATE_VALVE_ADDS")]
    [InlineData("garbage")]
    public void Parse_never_throws_and_degrades_to_unknown(string? raw)
    {
        var act = () => GameStateParser.Parse(raw);

        act.Should().NotThrow();
        act().Should().Be(GameState.Unknown);
    }
}
