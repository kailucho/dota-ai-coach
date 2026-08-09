namespace DotaAiCoach.FairPlay;

/// <summary>
/// The six data-legitimacy categories a live-facing data point must be
/// classified under, per this project's dota-fairplay policy. See the
/// project's docs/FAIR_PLAY.md for the full definitions.
/// </summary>
public enum FairPlayClassification
{
    /// <summary>Visible to the player right now through their own GSI feed.</summary>
    LiveAllowed,

    /// <summary>Rendered on the player's own screen but not exposed via GSI; must
    /// be captured from the actual rendered frame, never substituted.</summary>
    VisibleScreen,

    /// <summary>Public information independent of the current live match
    /// (patch notes, static hero/item data, public match history).</summary>
    PublicPreflight,

    /// <summary>Only legitimately available after the match ends (e.g. full
    /// replay data). Must never reach a live recommendation path.</summary>
    PostgameOnly,

    /// <summary>Available only through spectator/observer mode, which by
    /// design sees more than a player does. Never usable for a live
    /// player-facing feature.</summary>
    SpectatorOnly,

    /// <summary>Not legitimately available to a player through any sanctioned
    /// means. Must never be captured, stored, or used.</summary>
    Forbidden
}
