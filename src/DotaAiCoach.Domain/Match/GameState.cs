namespace DotaAiCoach.Domain.Match;

/// <summary>
/// The match's current phase, mirroring GSI's <c>map.game_state</c> string
/// enum. Valve does not publish an official schema, so this enum is a
/// closed set of currently-observed values plus <see cref="Unknown"/> as a
/// mandatory fallback — see <see cref="GameStateParser"/>.
/// </summary>
public enum GameState
{
    Unknown = 0,
    Init,
    WaitForPlayersToLoad,
    HeroSelection,
    StrategyTime,
    TeamShowcase,
    WaitForMapToLoad,
    PreGame,
    GameInProgress,
    PostGame,
    Disconnect
}
