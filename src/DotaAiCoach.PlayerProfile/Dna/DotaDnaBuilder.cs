using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.PlayerProfile.Dna;

/// <summary>
/// Builds a DotaDnaProfile that always reports all eleven dimensions.
/// Farm/Fighting/Survival/LateGame are computable from OpenDota's unparsed
/// match summaries. Laning/Objectives/Vision/Itemization/BuybackDiscipline/
/// Tempo become computable once parsed match detail is available (see
/// ParsedMatchDetail, populated only via an explicit, user-triggered Deep
/// DNA Refresh — see docs/adr/0016) — parsedMatches defaults to empty, in
/// which case these six report InsufficientData rather than a fabricated
/// score. Positioning stays deferred even with parsed data: OpenDota's
/// lane_pos/position_est are heatmap bucket counts, not a real trajectory
/// signal, and computing a "Positioning" score from them would be
/// misleading — see docs/adr/0016.
/// </summary>
public sealed class DotaDnaBuilder
{
    private const int MinSamples = 5;

    public DotaDnaProfile Build(
        ProfileAvailability availability,
        IReadOnlyList<PlayerMatchSummary> matches,
        int overallGames,
        int overallWins,
        IReadOnlyList<ParsedMatchDetail>? parsedMatches = null)
    {
        if (availability != ProfileAvailability.Available)
        {
            return new DotaDnaProfile { Dimensions = AllDeferred("profile_unavailable") };
        }

        var rankedMatches = matches.Where(m => !m.IsTurbo).ToArray();
        var parsed = parsedMatches ?? Array.Empty<ParsedMatchDetail>();

        var dimensions = new List<DnaDimensionScore>
        {
            BuildFarm(rankedMatches),
            BuildFighting(rankedMatches),
            BuildSurvival(rankedMatches),
            BuildLateGame(rankedMatches, overallGames, overallWins),
            BuildTempo(parsed),
            BuildLaning(parsed),
            BuildObjectives(parsed),
            BuildVision(parsed),
            BuildItemization(parsed),
            BuildBuybackDiscipline(parsed),
            Deferred(DotaDnaDimension.Positioning, DnaAvailability.NotYetImplemented,
                "Parsed match position data (lane_pos/position_est) is a heatmap of grid bucket counts, not a real trajectory signal — computing a Positioning score from it would be misleading. See docs/adr/0016."),
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

    private static DnaDimensionScore BuildTempo(IReadOnlyList<ParsedMatchDetail> parsed)
    {
        // Fraction of final gold reached by the ~10-minute mark (gold_t is
        // per-minute) — captures "snowbally fast starter" vs. "slow
        // scaler" in a way duration-distribution alone never could.
        var samples = parsed.Where(m => m.GoldTimeSeries.Count > 10).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Tempo, samples.Length);
        }

        var earlyShares = samples
            .Select(m => (double)m.GoldTimeSeries[10] / Math.Max(1, m.GoldTimeSeries[^1]))
            .ToArray();

        return Computed(DotaDnaDimension.Tempo, earlyShares.Average() * 100.0, samples.Length,
            "Share of final gold already reached by minute 10, averaged across parsed matches.");
    }

    private static DnaDimensionScore BuildLaning(IReadOnlyList<ParsedMatchDetail> parsed)
    {
        var samples = parsed.Where(m => m.LaneEfficiencyPct is not null).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Laning, samples.Length);
        }

        var average = samples.Average(m => m.LaneEfficiencyPct!.Value);
        return Computed(DotaDnaDimension.Laning, average, samples.Length,
            "OpenDota's own lane efficiency metric, averaged across parsed matches.");
    }

    private static DnaDimensionScore BuildObjectives(IReadOnlyList<ParsedMatchDetail> parsed)
    {
        var samples = parsed.Where(m => m.TowerKills is not null || m.RoshanKills is not null).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Objectives, samples.Length);
        }

        var scores = samples.Select(m => (double)((m.TowerKills ?? 0) * 2 + (m.RoshanKills ?? 0) * 3)).ToArray();
        var normalized = NormalizeSelfRelative(scores);
        return Computed(DotaDnaDimension.Objectives, normalized, samples.Length,
            "Tower and Roshan kills relative to your own recent average, from parsed matches.");
    }

    private static DnaDimensionScore BuildVision(IReadOnlyList<ParsedMatchDetail> parsed)
    {
        var samples = parsed.Where(m => m.ObsPlaced is not null || m.SenPlaced is not null).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Vision, samples.Length);
        }

        var scores = samples.Select(m => (double)((m.ObsPlaced ?? 0) + (m.SenPlaced ?? 0))).ToArray();
        var normalized = NormalizeSelfRelative(scores);
        return Computed(DotaDnaDimension.Vision, normalized, samples.Length,
            "Observer and sentry wards placed relative to your own recent average, from parsed matches.");
    }

    private static DnaDimensionScore BuildItemization(IReadOnlyList<ParsedMatchDetail> parsed)
    {
        var samples = parsed.Where(m => m.PurchaseTimesSeconds.Count > 0).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.Itemization, samples.Length);
        }

        // Purchases in the first 10 minutes, self-relative — a role-
        // agnostic proxy for purchase cadence rather than assuming any
        // specific "key item" (which is hero/role dependent).
        var earlyPurchaseCounts = samples
            .Select(m => (double)m.PurchaseTimesSeconds.Count(t => t <= 600))
            .ToArray();
        var normalized = NormalizeSelfRelative(earlyPurchaseCounts);
        return Computed(DotaDnaDimension.Itemization, normalized, samples.Length,
            "Purchases made in the first 10 minutes relative to your own recent average, from parsed matches.");
    }

    private static DnaDimensionScore BuildBuybackDiscipline(IReadOnlyList<ParsedMatchDetail> parsed)
    {
        var samples = parsed.Where(m => m.BuybackTimesSeconds is not null).ToArray();
        if (samples.Length < MinSamples)
        {
            return Insufficient(DotaDnaDimension.BuybackDiscipline, samples.Length);
        }

        // Fewer buybacks per match is treated as more disciplined (buying
        // back is usually a sign a fight or push was lost expensively) —
        // inverted the same way Survival inverts deaths per minute.
        var buybackCounts = samples.Select(m => (double)m.BuybackTimesSeconds.Count).ToArray();
        var maxCount = buybackCounts.Max();
        if (maxCount <= 0)
        {
            return Computed(DotaDnaDimension.BuybackDiscipline, 100, samples.Length,
                "No buybacks recorded in the sampled parsed matches.");
        }

        var inverted = buybackCounts.Select(c => 1.0 - c / maxCount).ToArray();
        return Computed(DotaDnaDimension.BuybackDiscipline, inverted.Average() * 100.0, samples.Length,
            "Buybacks per match, inverted, relative to your own recent range, from parsed matches.");
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
