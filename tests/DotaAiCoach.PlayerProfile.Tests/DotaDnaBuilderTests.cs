using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Model;
using FluentAssertions;

namespace DotaAiCoach.PlayerProfile.Tests;

public class DotaDnaBuilderTests
{
    private static PlayerMatchSummary Match(int gpm, int xpm, int kills, int assists, int heroDamage, int deaths, int durationSeconds, bool won) =>
        new()
        {
            GoldPerMin = gpm,
            XpPerMin = xpm,
            Kills = kills,
            Assists = assists,
            HeroDamage = heroDamage,
            Deaths = deaths,
            DurationSeconds = durationSeconds,
            RadiantWin = won,
            PlayerSlot = 0,
            PlayerWon = won,
            IsTurbo = false,
        };

    [Fact]
    public void All_eleven_dimensions_are_always_emitted_so_the_ui_can_explain_what_is_missing()
    {
        var builder = new DotaDnaBuilder();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), overallGames: 0, overallWins: 0);

        profile.Dimensions.Should().HaveCount(Enum.GetValues<DotaDnaDimension>().Length);
    }

    [Fact]
    public void Vision_positioning_itemization_and_buyback_are_marked_requiring_parsed_matches_not_scored_zero()
    {
        var builder = new DotaDnaBuilder();
        var matches = Enumerable.Range(0, 10).Select(_ => Match(500, 500, 5, 5, 10000, 2, 1800, true)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, matches, overallGames: 10, overallWins: 5);

        var vision = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Vision);
        var itemization = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Itemization);
        var buyback = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.BuybackDiscipline);
        var positioning = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Positioning);

        vision.Score.Should().BeNull();
        vision.Availability.Should().Be(DnaAvailability.RequiresParsedMatches);
        itemization.Availability.Should().Be(DnaAvailability.RequiresParsedMatches);
        buyback.Availability.Should().Be(DnaAvailability.RequiresParsedMatches);
        positioning.Availability.Should().Be(DnaAvailability.RequiresReplay);
    }

    [Fact]
    public void Farm_dimension_is_null_when_fewer_than_five_matches_carry_gold_per_min()
    {
        var builder = new DotaDnaBuilder();
        var matches = new[] { Match(500, 500, 1, 1, 1000, 1, 1800, true) };

        var profile = builder.Build(ProfileAvailability.Available, matches, overallGames: 1, overallWins: 1);

        var farm = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Farm);
        farm.Score.Should().BeNull();
        farm.Availability.Should().Be(DnaAvailability.InsufficientData);
    }

    [Fact]
    public void Farm_dimension_is_computed_when_enough_recent_matches_carry_gold_per_min()
    {
        var builder = new DotaDnaBuilder();
        var matches = Enumerable.Range(0, 6).Select(_ => Match(500, 500, 5, 5, 10000, 2, 1800, true)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, matches, overallGames: 6, overallWins: 5);

        var farm = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Farm);
        farm.Score.Should().NotBeNull();
        farm.Availability.Should().Be(DnaAvailability.Computed);
    }

    [Fact]
    public void Strengths_and_weaknesses_are_empty_when_fewer_than_two_dimensions_are_computed()
    {
        var builder = new DotaDnaBuilder();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), overallGames: 0, overallWins: 0);

        profile.PrimaryStrengths.Should().BeEmpty();
        profile.PrimaryWeaknesses.Should().BeEmpty();
    }
}
