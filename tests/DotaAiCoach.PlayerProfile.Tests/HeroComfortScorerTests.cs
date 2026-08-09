using DotaAiCoach.PlayerProfile.Model;
using DotaAiCoach.PlayerProfile.Scoring;
using FluentAssertions;

namespace DotaAiCoach.PlayerProfile.Tests;

public class HeroComfortScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    private static HeroComfortScorer CreateScorer() =>
        new(timeProvider: new FakeTimeProvider(Now));

    [Fact]
    public void Two_games_two_wins_does_not_produce_comfort_100_because_shrinkage_and_the_volume_cap_both_bound_it()
    {
        var scorer = CreateScorer();
        var stats = new HeroProfileStats { HeroId = 1, Games = 2, Wins = 2, LastPlayed = Now };

        var result = scorer.Score(ProfileAvailability.Available, heroId: 1, stats,
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        result.Score.Should().NotBeNull();
        result.Score!.Value.Should().BeLessThan(70,
            "the MASTER_PLAN explicitly requires that 2 games / 2 wins must not produce comfort 100");
    }

    [Fact]
    public void Two_games_two_wins_scores_below_forty_games_at_fifty_five_percent_because_evidence_outweighs_a_lucky_streak()
    {
        var scorer = CreateScorer();

        var luckyTwo = scorer.Score(ProfileAvailability.Available, heroId: 1,
            new HeroProfileStats { HeroId = 1, Games = 2, Wins = 2, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        var steadyForty = scorer.Score(ProfileAvailability.Available, heroId: 1,
            new HeroProfileStats { HeroId = 1, Games = 40, Wins = 22, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        luckyTwo.Score!.Value.Should().BeLessThan(steadyForty.Score!.Value);
    }

    [Fact]
    public void Zero_games_on_a_hero_returns_null_score_because_absent_data_is_undecidable_not_zero()
    {
        var scorer = CreateScorer();

        var result = scorer.Score(ProfileAvailability.Available, heroId: 1,
            new HeroProfileStats { HeroId = 1, Games = 0, Wins = 0 },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        result.Score.Should().BeNull();
        result.PrimaryReason.Should().Be("no_games_on_hero");
    }

    [Fact]
    public void Private_profile_returns_null_score_rather_than_a_default()
    {
        var scorer = CreateScorer();

        var result = scorer.Score(ProfileAvailability.MatchHistoryPrivate, heroId: 1,
            heroStats: null, overallGames: 0, overallWins: 0, heroMatches: Array.Empty<PlayerMatchSummary>());

        result.Score.Should().BeNull();
        result.PrimaryReason.Should().Be("profile_unavailable");
    }

    [Fact]
    public void A_hero_last_played_two_years_ago_scores_lower_than_the_same_record_played_last_week()
    {
        var scorer = CreateScorer();
        var stats = new Func<DateTimeOffset, HeroProfileStats>(lastPlayed =>
            new HeroProfileStats { HeroId = 1, Games = 30, Wins = 16, LastPlayed = lastPlayed });

        var recent = scorer.Score(ProfileAvailability.Available, 1, stats(Now.AddDays(-7)),
            100, 50, Array.Empty<PlayerMatchSummary>());
        var stale = scorer.Score(ProfileAvailability.Available, 1, stats(Now.AddDays(-730)),
            100, 50, Array.Empty<PlayerMatchSummary>());

        recent.Score!.Value.Should().BeGreaterThan(stale.Score!.Value);
    }

    [Fact]
    public void Null_performance_component_redistributes_its_weight_instead_of_counting_as_zero()
    {
        var scorer = CreateScorer();
        // No matches supplied => PerformanceFactor/Consistency are null.
        var result = scorer.Score(ProfileAvailability.Available, heroId: 1,
            new HeroProfileStats { HeroId = 1, Games = 20, Wins = 10, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        result.Components.PerformanceFactor.Should().BeNull();
        result.Score.Should().NotBeNull().And.BeGreaterThan(0,
            "a null component should not zero out the whole score");
    }

    [Fact]
    public void Unknown_mechanical_complexity_applies_no_penalty_because_null_means_undecidable()
    {
        var scorer = CreateScorer();
        // Hero id far outside the curated HeroMechanicalComplexity seed set.
        var result = scorer.Score(ProfileAvailability.Available, heroId: 999,
            new HeroProfileStats { HeroId = 999, Games = 10, Wins = 5, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        result.Components.ComplexityPenalty.Should().BeNull();
    }

    [Fact]
    public void High_complexity_penalty_vanishes_at_high_game_counts_because_experience_offsets_difficulty()
    {
        var scorer = CreateScorer();
        // Hero 74 (Invoker) is seeded at 0.9 complexity in HeroMechanicalComplexity.
        var lowVolume = scorer.Score(ProfileAvailability.Available, heroId: 74,
            new HeroProfileStats { HeroId = 74, Games = 3, Wins = 2, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());
        var highVolume = scorer.Score(ProfileAvailability.Available, heroId: 74,
            new HeroProfileStats { HeroId = 74, Games = 40, Wins = 22, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        lowVolume.Components.ComplexityPenalty!.Value.Should().BeLessThan(highVolume.Components.ComplexityPenalty!.Value);
        highVolume.Components.ComplexityPenalty!.Value.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Score_is_monotonic_in_games_when_winrate_is_held_constant()
    {
        var scorer = CreateScorer();
        var ten = scorer.Score(ProfileAvailability.Available, 1,
            new HeroProfileStats { HeroId = 1, Games = 10, Wins = 5, LastPlayed = Now },
            100, 50, Array.Empty<PlayerMatchSummary>());
        var thirty = scorer.Score(ProfileAvailability.Available, 1,
            new HeroProfileStats { HeroId = 1, Games = 30, Wins = 15, LastPlayed = Now },
            100, 50, Array.Empty<PlayerMatchSummary>());

        thirty.Score!.Value.Should().BeGreaterThan(ten.Score!.Value);
    }

    [Fact]
    public void Confidence_is_reported_separately_and_never_folded_into_the_score()
    {
        var scorer = CreateScorer();
        var result = scorer.Score(ProfileAvailability.Available, heroId: 1,
            new HeroProfileStats { HeroId = 1, Games = 2, Wins = 2, LastPlayed = Now },
            overallGames: 100, overallWins: 50, heroMatches: Array.Empty<PlayerMatchSummary>());

        result.Confidence.Should().NotBeNull();
        result.Confidence!.Value.Should().BeLessThan(1.0);
        result.Score.Should().NotBe(result.Confidence * 100);
    }
}
