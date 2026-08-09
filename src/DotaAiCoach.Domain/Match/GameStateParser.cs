namespace DotaAiCoach.Domain.Match;

/// <summary>
/// Parses GSI's <c>map.game_state</c> string into <see cref="GameState"/>.
/// Never throws: an unrecognized string (Valve can add new states at any
/// time, and no official spec exists) degrades to
/// <see cref="GameState.Unknown"/> rather than crashing the ingestion
/// pipeline — per dota-gsi's "never invent GSI fields, never throw on an
/// unmatched enum value" rule. Callers that need the original text should
/// keep it separately (see <see cref="MatchSnapshot.RawGameState"/>).
/// </summary>
public static class GameStateParser
{
    public static GameState Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return GameState.Unknown;
        }

        return raw switch
        {
            "DOTA_GAMERULES_STATE_INIT" => GameState.Init,
            "DOTA_GAMERULES_STATE_WAIT_FOR_PLAYERS_TO_LOAD" => GameState.WaitForPlayersToLoad,
            "DOTA_GAMERULES_STATE_HERO_SELECTION" => GameState.HeroSelection,
            "DOTA_GAMERULES_STATE_STRATEGY_TIME" => GameState.StrategyTime,
            "DOTA_GAMERULES_STATE_TEAM_SHOWCASE" => GameState.TeamShowcase,
            "DOTA_GAMERULES_STATE_WAIT_FOR_MAP_TO_LOAD" => GameState.WaitForMapToLoad,
            "DOTA_GAMERULES_STATE_PRE_GAME" => GameState.PreGame,
            "DOTA_GAMERULES_STATE_GAME_IN_PROGRESS" => GameState.GameInProgress,
            "DOTA_GAMERULES_STATE_POST_GAME" => GameState.PostGame,
            "DOTA_GAMERULES_STATE_DISCONNECT" => GameState.Disconnect,
            _ => GameState.Unknown
        };
    }
}
