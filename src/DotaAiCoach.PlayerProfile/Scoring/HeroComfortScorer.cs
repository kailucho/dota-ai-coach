using DotaAiCoach.PlayerProfile.Model;

namespace DotaAiCoach.PlayerProfile.Scoring;

/// <summary>
/// Computes a 0-100 Hero Comfort Score per docs/adr/0015. Two defenses
/// against overconfidence on small samples: Bayesian shrinkage of the win
/// rate toward the player's own overall prior, and a hard cap tied to
/// evidence volume so a lucky small sample cannot reach 100 regardless of
/// how the weighted components combine.
/// </summary>
public sealed class HeroComfortScorer
{
    private readonly HeroComfortOptions _options;
    private readonly TimeProvider _timeProvider;

    public HeroComfortScorer(HeroComfortOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new HeroComfortOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public HeroComfortScore Score(
        ProfileAvailability availability,
        int heroId,
        HeroProfileStats? heroStats,
        int overallGames,
        int overallWins,
        IReadOnlyList<PlayerMatchSummary> heroMatches)
    {
        if (availability != ProfileAvailability.Available)
        {
            return new HeroComfortScore { HeroId = heroId, PrimaryReason = "profile_unavailable" };
        }

        var games = heroStats?.Games ?? 0;
        if (games <= 0)
        {
            return new HeroComfortScore { HeroId = heroId, PrimaryReason = "no_games_on_hero" };
        }

        var wins = heroStats!.Wins ?? 0;

        var adjustedWinRate = ComputeAdjustedWinRate(overallGames, overallWins, games, wins);
        var volume = ComputeVolumeFactor(games);
        var recency = ComputeRecencyFactor(heroStats.LastPlayed);

        var rankedMatches = heroMatches.Where(m => !m.IsTurbo).ToArray();
        var performanceSamples = rankedMatches.Where(m => m.GoldPerMin is not null || m.XpPerMin is not null).ToArray();

        var performance = ComputePerformanceFactor(performanceSamples, overallPerHeroBaseline: null);
        var consistency = ComputeConsistency(performanceSamples);
        var roleFamiliarity = ComputeRoleFamiliarity(rankedMatches);
        var patchFamiliarity = ComputePatchFamiliarity(rankedMatches);
        var complexity = HeroMechanicalComplexity.Get(heroId);
        var complexityPenalty = complexity is null ? (double?)null : 1.0 - complexity.Value * (1.0 - volume);

        var components = new ComfortComponents
        {
            AdjustedWinRate = adjustedWinRate,
            VolumeFactor = volume,
            RecencyFactor = recency,
            PerformanceFactor = performance,
            Consistency = consistency,
            RoleFamiliarity = roleFamiliarity,
            PatchFamiliarity = patchFamiliarity,
            ComplexityPenalty = complexityPenalty,
            MatchupFamiliarity = null,
        };

        var weighted = CombineWeighted(components);
        var cap = 100.0 * (0.55 + 0.45 * volume);
        var score = Math.Min(weighted * 100.0, cap);

        var confidence = ComputeConfidence(components, volume);

        return new HeroComfortScore
        {
            HeroId = heroId,
            Score = Math.Round(Math.Clamp(score, 0, 100), 1),
            Confidence = confidence,
            Components = components,
        };
    }

    private double ComputeAdjustedWinRate(int overallGames, int overallWins, int games, int wins)
    {
        var k0 = _options.PriorStrengthOverall;
        var mu = (overallWins + k0 * 0.5) / (overallGames + k0);

        var k = _options.PriorStrength;
        var alpha = mu * k;
        var beta = (1.0 - mu) * k;

        var pHat = (wins + alpha) / (games + alpha + beta);

        var nEff = games + k;
        var z = _options.ConfidenceZ;
        var z2 = z * z;

        var denominator = 1.0 + z2 / nEff;
        var center = pHat + z2 / (2 * nEff);
        var margin = z * Math.Sqrt(pHat * (1 - pHat) / nEff + z2 / (4 * nEff * nEff));
        var lowerBound = (center - margin) / denominator;

        return Math.Clamp(lowerBound, 0, 1);
    }

    private double ComputeVolumeFactor(int games)
    {
        var saturation = Math.Max(1, _options.VolumeSaturationGames);
        var value = Math.Log(1 + games) / Math.Log(1 + saturation);
        return Math.Clamp(value, 0, 1);
    }

    private double? ComputeRecencyFactor(DateTimeOffset? lastPlayed)
    {
        if (lastPlayed is null)
        {
            return null;
        }

        var daysSince = (_timeProvider.GetUtcNow() - lastPlayed.Value).TotalDays;
        if (daysSince < 0)
        {
            daysSince = 0;
        }

        var halfLife = Math.Max(1, _options.RecencyHalfLifeDays);
        var tau = halfLife / Math.Log(2);
        return Math.Clamp(Math.Exp(-daysSince / tau), 0, 1);
    }

    private double? ComputePerformanceFactor(IReadOnlyList<PlayerMatchSummary> samples, double? overallPerHeroBaseline)
    {
        if (samples.Count < _options.MinPerformanceSamples)
        {
            return null;
        }

        // Self-referential: percentile of this hero's mean GPM+XPM against
        // the player's own observed range in the sample, since no
        // bracket-wide baseline is available from unparsed match summaries.
        var scores = samples
            .Select(m => (double)((m.GoldPerMin ?? 0) + (m.XpPerMin ?? 0)))
            .ToArray();

        var max = scores.Max();
        if (max <= 0)
        {
            return null;
        }

        var mean = scores.Average();
        return Math.Clamp(mean / max, 0, 1);
    }

    private double? ComputeConsistency(IReadOnlyList<PlayerMatchSummary> samples)
    {
        if (samples.Count < _options.MinPerformanceSamples)
        {
            return null;
        }

        var scores = samples
            .Select(m => (double)((m.GoldPerMin ?? 0) + (m.XpPerMin ?? 0)))
            .ToArray();

        var mean = scores.Average();
        if (mean <= 0)
        {
            return null;
        }

        var variance = scores.Select(s => (s - mean) * (s - mean)).Average();
        var stdev = Math.Sqrt(variance);
        var coefficientOfVariation = stdev / mean;

        return Math.Clamp(1.0 - coefficientOfVariation / _options.ConsistencySpread, 0, 1);
    }

    private double? ComputeRoleFamiliarity(IReadOnlyList<PlayerMatchSummary> matches)
    {
        var withRole = matches.Where(m => m.LaneRole is not null).ToArray();
        if (withRole.Length < _options.MinRoleFamiliaritySamples)
        {
            return null;
        }

        var mostCommon = withRole
            .GroupBy(m => m.LaneRole!.Value)
            .OrderByDescending(g => g.Count())
            .First();

        return (double)mostCommon.Count() / withRole.Length;
    }

    private double? ComputePatchFamiliarity(IReadOnlyList<PlayerMatchSummary> matches)
    {
        var withStartTime = matches.Where(m => m.StartTime is not null).ToArray();
        if (withStartTime.Length == 0)
        {
            return null;
        }

        var windowStart = _timeProvider.GetUtcNow() - TimeSpan.FromDays(_options.PatchWindowDays);
        var withinWindow = withStartTime.Count(m => m.StartTime!.Value >= windowStart);

        return (double)withinWindow / withStartTime.Length;
    }

    private double CombineWeighted(ComfortComponents components)
    {
        var terms = new (double? Value, double Weight)[]
        {
            (components.AdjustedWinRate, _options.WeightAdjustedWinRate),
            (components.VolumeFactor, _options.WeightVolume),
            (components.RecencyFactor, _options.WeightRecency),
            (components.PerformanceFactor, _options.WeightPerformance),
            (components.Consistency, _options.WeightConsistency),
            (components.PatchFamiliarity, _options.WeightPatchFamiliarity),
            (components.RoleFamiliarity, _options.WeightRoleFamiliarity),
        };

        var availableWeight = terms.Where(t => t.Value is not null).Sum(t => t.Weight);
        if (availableWeight <= 0)
        {
            return 0;
        }

        var weightedSum = terms.Where(t => t.Value is not null).Sum(t => t.Value!.Value * t.Weight);
        var combined = weightedSum / availableWeight;

        return combined * (components.ComplexityPenalty ?? 1.0);
    }

    private static double ComputeConfidence(ComfortComponents components, double volume)
    {
        var terms = new[]
        {
            components.AdjustedWinRate, components.VolumeFactor, components.RecencyFactor,
            components.PerformanceFactor, components.Consistency, components.PatchFamiliarity,
            components.RoleFamiliarity,
        };

        var availableCount = terms.Count(t => t is not null);
        var completeness = (double)availableCount / terms.Length;

        return Math.Clamp(completeness * volume, 0, 1);
    }
}
