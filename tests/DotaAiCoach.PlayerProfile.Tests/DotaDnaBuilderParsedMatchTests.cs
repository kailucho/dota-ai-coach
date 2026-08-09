using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Model;
using FluentAssertions;

namespace DotaAiCoach.PlayerProfile.Tests;

public class DotaDnaBuilderParsedMatchTests
{
    private static ParsedMatchDetail Detail(
        long matchId = 1,
        IReadOnlyList<int>? purchaseTimes = null,
        IReadOnlyList<int>? buybackTimes = null,
        int? obsPlaced = 4,
        int? senPlaced = 4,
        double? laneEfficiencyPct = 60,
        int? towerKills = 1,
        int? roshanKills = 0,
        IReadOnlyList<int>? goldTimeSeries = null) =>
        new()
        {
            MatchId = matchId,
            PurchaseTimesSeconds = purchaseTimes ?? new[] { -90, 100, 300 },
            BuybackTimesSeconds = buybackTimes ?? Array.Empty<int>(),
            ObsPlaced = obsPlaced,
            SenPlaced = senPlaced,
            LaneEfficiencyPct = laneEfficiencyPct,
            TowerKills = towerKills,
            RoshanKills = roshanKills,
            GoldTimeSeries = goldTimeSeries ?? Enumerable.Range(0, 30).Select(i => i * 500).ToArray(),
        };

    [Fact]
    public void Six_dimensions_fall_back_to_insufficient_data_not_a_fabricated_score_when_parsed_matches_are_null()
    {
        var builder = new DotaDnaBuilder();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsedMatches: null);

        foreach (var dimension in new[]
                 {
                     DotaDnaDimension.Tempo, DotaDnaDimension.Laning, DotaDnaDimension.Objectives,
                     DotaDnaDimension.Vision, DotaDnaDimension.Itemization, DotaDnaDimension.BuybackDiscipline,
                 })
        {
            var score = profile.Dimensions.Single(d => d.Dimension == dimension);
            score.Score.Should().BeNull();
            score.Availability.Should().Be(DnaAvailability.InsufficientData);
        }
    }

    [Fact]
    public void All_eleven_dimensions_are_still_always_emitted_when_parsed_matches_are_supplied()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        profile.Dimensions.Should().HaveCount(Enum.GetValues<DotaDnaDimension>().Length);
    }

    [Fact]
    public void Buyback_discipline_is_computed_from_buyback_log_when_parsed_matches_are_available()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i, buybackTimes: Array.Empty<int>())).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var buyback = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.BuybackDiscipline);
        buyback.Availability.Should().Be(DnaAvailability.Computed);
        buyback.Score.Should().Be(100, "zero buybacks across all sampled matches should score as maximally disciplined");
    }

    [Fact]
    public void Itemization_is_computed_from_purchase_log_when_parsed_matches_are_available()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var itemization = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Itemization);
        itemization.Availability.Should().Be(DnaAvailability.Computed);
        itemization.Score.Should().NotBeNull();
    }

    [Fact]
    public void Vision_is_computed_from_obs_placed_and_sen_placed_when_parsed_matches_are_available()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var vision = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Vision);
        vision.Availability.Should().Be(DnaAvailability.Computed);
    }

    [Fact]
    public void Laning_is_computed_from_lane_efficiency_pct_when_parsed_matches_are_available()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i, laneEfficiencyPct: 47)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var laning = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Laning);
        laning.Availability.Should().Be(DnaAvailability.Computed);
        laning.Score.Should().Be(47);
    }

    [Fact]
    public void Objectives_is_computed_from_tower_kills_and_roshan_kills_when_parsed_matches_are_available()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var objectives = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Objectives);
        objectives.Availability.Should().Be(DnaAvailability.Computed);
    }

    [Fact]
    public void Tempo_is_computed_from_gold_t_time_series_when_parsed_matches_are_available()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var tempo = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Tempo);
        tempo.Availability.Should().Be(DnaAvailability.Computed);
    }

    [Fact]
    public void Positioning_remains_deferred_even_when_parsed_matches_are_available_because_lane_pos_is_a_heatmap_not_a_real_signal()
    {
        var builder = new DotaDnaBuilder();
        var parsed = Enumerable.Range(0, 6).Select(i => Detail(matchId: i)).ToArray();

        var profile = builder.Build(ProfileAvailability.Available, Array.Empty<PlayerMatchSummary>(), 0, 0, parsed);

        var positioning = profile.Dimensions.Single(d => d.Dimension == DotaDnaDimension.Positioning);
        positioning.Score.Should().BeNull();
        positioning.Availability.Should().Be(DnaAvailability.NotYetImplemented);
    }
}
