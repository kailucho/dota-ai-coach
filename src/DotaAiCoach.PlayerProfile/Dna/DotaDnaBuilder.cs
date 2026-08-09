using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.PlayerProfile.Dna;

/// <summary>
/// Builds a DotaDnaProfile that always reports all eleven dimensions.
/// Only Farm/Fighting/Survival/LateGame are computable from OpenDota's
/// unparsed match summaries; the rest are marked with a truthful
/// DnaAvailability rather than a fabricated score — see docs/adr/0014.
/// </summary>
public sealed class DotaDnaBuilder
{
    private const int MinSamples = 5;

    public DotaDnaProfile Build(ProfileAvailability availability, IReadOnlyList<PlayerMatchSummary> matches, int overallGames, int overallWins)
    {
        if (availability != ProfileAvailability.Available)
        {
            return new DotaDnaProfile { Dimensions = AllDeferred("profile_unavailable") };
        }

        var rankedMatches = matches.Where(m => !m.IsTurbo).ToArray();

        var dimensions = new List<DnaDimensionScore>
        {
            BuildFarm(rankedMatches),
            BuildFighting(rankedMatches),
            BuildSurvival(rankedMatches),
            BuildLateGame(rankedMatches, overallGames, overallWins),
            Deferred(DotaDnaDimension.Tempo, DnaAvailability.NotYetImplemented,
                "Only proxy available (match duration distribution) is too thin to name as tempo — deferred, see docs/adr/0014."),
            Deferred(DotaDnaDimension.Laning, DnaAvailability.RequiresParsedMatches,
                "Lane efficiency requires per-minute timeline data from a parsed match."),
            Deferred(DotaDnaDimension.Objectives, DnaAvailability.RequiresParsedMatches,
                "Roshan/tower-take timing requires parsed match detail; tower_damage alone is not a reliable proxy."),
            Deferred(DotaDnaDimension.Vision, DnaAvailability.RequiresParsedMatches,
                "Ward counts (obs_placed/sen_placed) are only present on parsed match detail."),
            Deferred(DotaDnaDimension.Itemization, DnaAvailability.RequiresParsedMatches,
                "Purchase timing (purchase_log) is only present on parsed match detail."),
            Deferred(DotaDnaDimension.BuybackDiscipline, DnaAvailability.RequiresParsedMatches,
                "Buyback usage (buyback_log) is only present on parsed match detail."),
            Deferred(DotaDnaDimension.Positioning, DnaAvailability.RequiresReplay,
                "Positioning is not derivable from any API summary field; requires raw replay analysis."),
        };

        var computed = dimensions.Where(d => d.Availability == DnaAvailability.Computed).ToArray();

        var strengths = computed.Length >= 2
            ? computed.OrderByDescending(d => d.Score).Take(2).Select(d => d.Dimension).ToArray()
            : Array.Empty<DotaDnaDimension>();
        var weaknesses = computed.Length >= 2
            ? computed.OrderBy(d => d.Score).Take(2).Select(d => d.Dimension).ToArray()
            : Array.Empty<DotaDnaDimension>();

        return new DotaDnaProfile
        {
            Dimensions = dimensions,
            PrimaryStrengths = strengths,
            PrimaryWeaknesses = weaknesses,
        };
    }

    private static DnaDimensionScore BuildFarm(IReadOnlyList<PlayerMatchSummary> matches)
    {
        var samples = matches.Where(m => m.GoldPerMin is not null).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Farm, samples.Length);
        }

        var normalized = NormalizeSelfRelative(samples.Select(m => (double)m.GoldPerMin!.Value).ToArray());
        return Computed(DotaDnaDimension.Farm, normalized, samples.Length, "Gold-per-minute relative to your own recent average.");
    }

    private static DnaDimensionScore BuildFighting(IReadOnlyList<PlayerMatchSummary> matches)
    {
        var samples = matches.Where(m => m.Kills is not null && m.Assists is not null && m.HeroDamage is not null).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Fighting, samples.Length);
        }

        var normalized = NormalizeSelfRelative(samples
            .Select(m => (double)(m.Kills!.Value + m.Assists!.Value) + m.HeroDamage!.Value / 1000.0)
            .ToArray());
        return Computed(DotaDnaDimension.Fighting, normalized, samples.Length, "Kill participation and hero damage relative to your own recent average.");
    }

    private static DnaDimensionScore BuildSurvival(IReadOnlyList<PlayerMatchSummary> matches)
    {
        var samples = matches.Where(m => m.Deaths is not null && m.DurationSeconds is > 0).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Survival, samples.Length);
        }

        // Deaths per 10 minutes, inverted (fewer deaths => higher score).
        var deathRates = samples.Select(m => m.Deaths!.Value / (m.DurationSeconds!.Value / 600.0)).ToArray();
        var maxRate = deathRates.Max();
        if (maxRate <= 0)
        {
            return Computed(DotaDnaDimension.Survival, 100, samples.Length, "No deaths recorded in the sampled matches.");
        }

        var inverted = deathRates.Select(r => 1.0 - r / maxRate).ToArray();
        var score = inverted.Average() * 100.0;
        return Computed(DotaDnaDimension.Survival, score, samples.Length, "Deaths per 10 minutes, inverted, relative to your own recent range.");
    }

    private static DnaDimensionScore BuildLateGame(IReadOnlyList<PlayerMatchSummary> matches, int overallGames, int overallWins)
    {
        var longGames = matches.Where(m => m.DurationSeconds is > 40 * 60 && m.PlayerWon is not null).ToArray();
        if (longGames.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.LateGame, longGames.Length);
        }

        var wins = longGames.Count(m => m.PlayerWon == true);
        var mu = overallGames > 0 ? (double)overallWins / overallGames : 0.5;
        const double k = 10.0;
        var pHat = (wins + mu * k) / (longGames.Length + k);

        return Computed(DotaDnaDimension.LateGame, pHat * 100.0, longGames.Length,
            "Shrunk win rate in matches lasting over 40 minutes.");
    }

    private static double NormalizeSelfRelative(IReadOnlyList<double> values)
    {
        var max = values.Max();
        if (max <= 0)
        {
            return 0;
        }

        return values.Average() / max * 100.0;
    }

    private static DnaDimensionScore Computed(DotaDnaDimension dimension, double score, int sampleSize, string explanation) =>
        new()
        {
            Dimension = dimension,
            Score = Math.Round(Math.Clamp(score, 0, 100), 1),
            Availability = DnaAvailability.Computed,
            Explanation = explanation,
            SampleSize = sampleSize,
        };

    private static DnaDimensionScore Insufficient(DotaDnaDimension dimension, int sampleSize) =>
        new()
        {
            Dimension = dimension,
            Availability = DnaAvailability.InsufficientData,
            Explanation = $"Fewer than {MinSamples} recent matches carried the needed fields.",
            SampleSize = sampleSize,
        };

    private static DnaDimensionScore Deferred(DotaDnaDimension dimension, DnaAvailability availability, string explanation) =>
        new() { Dimension = dimension, Availability = availability, Explanation = explanation };

    private static IReadOnlyList<DnaDimensionScore> AllDeferred(string reasonCode) =>
        Enum.GetValues<DotaDnaDimension>()
            .Select(d => new DnaDimensionScore
            {
                Dimension = d,
                Availability = DnaAvailability.InsufficientData,
                Explanation = reasonCode,
            })
            .ToArray();
}
