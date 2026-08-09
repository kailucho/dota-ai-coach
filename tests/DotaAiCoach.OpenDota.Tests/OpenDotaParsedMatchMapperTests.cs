using DotaAiCoach.OpenDota;
using FluentAssertions;

namespace DotaAiCoach.OpenDota.Tests;

public class OpenDotaParsedMatchMapperTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static string LoadFixture(string name) => File.ReadAllText(FixturePath(name));

    // The real captured fixture's player[0] has account_id 261133384.
    private const long RealFixtureAccountId = 261133384;
    private const long RealFixtureMatchId = 8936354085;

    [Fact]
    public void Parsed_match_detail_is_deserialized_from_real_captured_fixture_without_field_mismatches()
    {
        var raw = LoadFixture("matches_8936354085_parsed.json");

        var detail = OpenDotaProfileMapper.MapParsedMatch(RealFixtureMatchId, raw, RealFixtureAccountId);

        detail.Should().NotBeNull();
        detail!.MatchId.Should().Be(RealFixtureMatchId);
        detail.HeroId.Should().Be(85);
    }

    [Fact]
    public void Parsed_match_detail_is_matched_to_the_requesting_player_by_account_id()
    {
        var raw = LoadFixture("matches_8936354085_parsed.json");

        var wrongPlayer = OpenDotaProfileMapper.MapParsedMatch(RealFixtureMatchId, raw, requestingAccountId32: 1);

        wrongPlayer.Should().BeNull("account_id 1 is not one of the ten players in this real match");
    }

    [Fact]
    public void Purchase_log_times_are_extracted_from_the_real_fixture()
    {
        var raw = LoadFixture("matches_8936354085_parsed.json");

        var detail = OpenDotaProfileMapper.MapParsedMatch(RealFixtureMatchId, raw, RealFixtureAccountId);

        detail!.PurchaseTimesSeconds.Should().NotBeEmpty();
        detail.PurchaseTimesSeconds.Should().Contain(-90, "the real fixture's first purchase (tango) was bought at time -90");
    }

    [Fact]
    public void Gold_and_xp_time_series_are_extracted_from_the_real_fixture()
    {
        var raw = LoadFixture("matches_8936354085_parsed.json");

        var detail = OpenDotaProfileMapper.MapParsedMatch(RealFixtureMatchId, raw, RealFixtureAccountId);

        detail!.GoldTimeSeries.Should().HaveCount(32);
        detail.XpTimeSeries.Should().HaveCount(32);
    }

    [Fact]
    public void Lane_efficiency_and_ward_counts_are_extracted_from_the_real_fixture()
    {
        var raw = LoadFixture("matches_8936354085_parsed.json");

        var detail = OpenDotaProfileMapper.MapParsedMatch(RealFixtureMatchId, raw, RealFixtureAccountId);

        detail!.LaneEfficiencyPct.Should().Be(47);
        detail.ObsPlaced.Should().Be(6);
        detail.SenPlaced.Should().Be(14);
    }

    [Fact]
    public void Malformed_json_returns_null_rather_than_throwing()
    {
        var act = () => OpenDotaProfileMapper.MapParsedMatch(RealFixtureMatchId, "{not valid json", RealFixtureAccountId);

        act.Should().NotThrow();
        act().Should().BeNull();
    }
}
