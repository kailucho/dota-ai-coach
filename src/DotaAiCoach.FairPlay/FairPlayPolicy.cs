namespace DotaAiCoach.FairPlay;

/// <summary>
/// The single source of truth for which classifications may legitimately
/// reach a live path. Per dota-fairplay's core rule: if a human player
/// could legitimately know something from their own game state or visible
/// screen, the application may analyze it; if Dota intentionally hides it,
/// the live application must not access it.
/// </summary>
public static class FairPlayPolicy
{
    /// <summary>
    /// Classifications that are safe to use in a live, in-game recommendation
    /// path. PostgameOnly, SpectatorOnly, and Forbidden are deliberately
    /// excluded — see dota-fairplay/references/classification-categories.md.
    /// </summary>
    public static readonly IReadOnlySet<FairPlayClassification> LiveAllowedSet =
        new HashSet<FairPlayClassification>
        {
            FairPlayClassification.LiveAllowed,
            FairPlayClassification.VisibleScreen,
            FairPlayClassification.PublicPreflight
        };

    public static bool IsAllowedLive(FairPlayClassification classification) =>
        LiveAllowedSet.Contains(classification);
}
