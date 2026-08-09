namespace DotaAiCoach.PlayerProfile.Scoring;

/// <summary>
/// Hand-curated 0-1 mechanical complexity per hero, used to dampen comfort
/// at low game counts. Unlisted heroes return null — a missing entry is
/// undecidable, and HeroComfortScorer must apply no penalty in that case,
/// never treat it as "zero complexity".
/// </summary>
public static class HeroMechanicalComplexity
{
    // A deliberately small seed set (mirrors ItemCatalog's "PATCH: verify"
    // placeholder pattern in RecommendationEngines) — expand as needed.
    private static readonly IReadOnlyDictionary<int, double> ByHeroId = new Dictionary<int, double>
    {
        [1] = 0.7,   // Anti-Mage — blink micro, farm pathing
        [7] = 0.3,   // Earthshaker
        [14] = 0.4,  // Pudge
        [22] = 0.5,  // Zeus
        [26] = 0.85, // Lion (positioning-heavy, low mechanical) — placeholder value
        [35] = 0.5,  // Sniper
        [60] = 0.4,  // Night Stalker
        [74] = 0.9,  // Invoker
        [86] = 0.8,  // Rubick
        [126] = 0.85, // Void Spirit
    };

    public static double? Get(int heroId) => ByHeroId.TryGetValue(heroId, out var value) ? value : null;
}
