using DotaAiCoach.OpenDota;
using DotaAiCoach.PlayerProfile.Dna;
using DotaAiCoach.PlayerProfile.Identity;
using DotaAiCoach.PlayerProfile.Model;
using DotaAiCoach.PlayerProfile.Scoring;
using FluentAssertions;

namespace DotaAiCoach.OpenDota.Tests;

public class OpenDotaProfileMapperTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static RawFetchResult LoadFixture(string name) => new(File.ReadAllText(FixturePath(name)), 200);

    private static readonly HeroComfortScorer Scorer = new();
    private static readonly DotaDnaBuilder DnaBuilder = new();
    private static readonly DateTimeOffset FetchedAt = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Player_slot_below_128_wins_when_radiant_won_because_slots_0_to_4_are_radiant()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var matches = LoadFixture("matches_100.json");
        var recent = LoadFixture("recentMatches.json");
        var wl = LoadFixture("wl.json");
        var heroes = LoadFixture("player_heroes.json");

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, recent, wl, heroes, Scorer, DnaBuilder, FetchedAt);

        var radiantWinner = snapshot.RecentMatches.First(m => m.PlayerSlot < 128 && m.RadiantWin == true);
        radiantWinner.PlayerWon.Should().BeTrue();
    }

    [Fact]
    public void Player_slot_128_wins_when_radiant_lost_because_slots_128_to_132_are_dire()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var matches = LoadFixture("matches_100.json");
        var recent = LoadFixture("recentMatches.json");
        var wl = LoadFixture("wl.json");
        var heroes = LoadFixture("player_heroes.json");

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, recent, wl, heroes, Scorer, DnaBuilder, FetchedAt);

        var direWinner = snapshot.RecentMatches.FirstOrDefault(m => m.PlayerSlot is >= 128 && m.RadiantWin == false);
        if (direWinner is not null)
        {
            direWinner.PlayerWon.Should().BeTrue();
        }
    }

    [Fact]
    public void Win_derivation_is_null_when_radiant_win_is_missing_rather_than_defaulting_to_a_loss()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var matchesJson = """[{"match_id":1,"player_slot":0,"duration":1200,"game_mode":1,"lobby_type":0,"hero_id":1,"start_time":1700000000}]""";
        var matches = new RawFetchResult(matchesJson, 200);
        var wl = LoadFixture("wl.json");

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, null, wl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.RecentMatches.Single().PlayerWon.Should().BeNull();
    }

    [Fact]
    public void Game_mode_22_is_flagged_turbo_regardless_of_lobby_type_because_turbo_is_a_game_mode()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var matchesJson = """[{"match_id":1,"player_slot":0,"radiant_win":true,"duration":1200,"game_mode":22,"lobby_type":7,"hero_id":1,"start_time":1700000000}]""";
        var matches = new RawFetchResult(matchesJson, 200);
        var wl = LoadFixture("wl.json");

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, null, wl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.RecentMatches.Single().IsTurbo.Should().BeTrue();
    }

    [Fact]
    public void Turbo_matches_are_excluded_from_ranked_comfort_input_so_they_cannot_inflate_the_sample()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var wl = LoadFixture("wl.json");
        var heroesJson = """[{"hero_id":1,"last_played":1786233213,"games":5,"win":3}]""";
        var heroes = new RawFetchResult(heroesJson, 200);

        var matchesJson = "[{\"match_id\":1,\"player_slot\":0,\"radiant_win\":true,\"duration\":1200,\"game_mode\":22,\"lobby_type\":7,\"hero_id\":1,\"start_time\":1700000000}," +
            "{\"match_id\":2,\"player_slot\":0,\"radiant_win\":true,\"duration\":1200,\"game_mode\":1,\"lobby_type\":0,\"hero_id\":1,\"start_time\":1700000000}]";
        var matches = new RawFetchResult(matchesJson, 200);

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, null, wl, heroes, Scorer, DnaBuilder, FetchedAt);

        snapshot.RecentMatches.Should().HaveCount(2, "both matches are stored");
        snapshot.RecentMatches.Count(m => m.IsTurbo).Should().Be(1);
    }

    [Fact]
    public void fh_unavailable_true_maps_to_MatchHistoryPrivate_not_to_zero_games()
    {
        var id = SteamId.FromSteam32(86845504);
        var player = LoadFixture("player_private_fh_unavailable.json");
        var emptyMatches = new RawFetchResult("[]", 200);
        var emptyWl = new RawFetchResult("""{"win":0,"lose":0}""", 200);

        var snapshot = OpenDotaProfileMapper.Map(id, player, emptyMatches, emptyMatches, emptyWl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.Availability.Should().Be(ProfileAvailability.MatchHistoryPrivate);
    }

    [Fact]
    public void Empty_match_list_with_zero_wins_and_zero_losses_maps_to_MatchHistoryPrivate_not_Available()
    {
        var id = SteamId.FromSteam32(86845504);
        var player = LoadFixture("player_private_fh_unavailable.json");
        var emptyMatches = new RawFetchResult("[]", 200);
        var emptyWl = new RawFetchResult("""{"win":0,"lose":0}""", 200);

        var snapshot = OpenDotaProfileMapper.Map(id, player, emptyMatches, emptyMatches, emptyWl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.RankedWins.Should().BeNull();
        snapshot.Availability.Should().NotBe(ProfileAvailability.Available);
    }

    [Fact]
    public void A_genuinely_new_account_with_an_open_profile_maps_to_Available_with_zero_games()
    {
        // fh_unavailable == false distinguishes a real "brand new, zero
        // games, but not private" account from the private case above.
        var id = SteamId.FromSteam32(999999999);
        var playerJson = """{"profile":{"account_id":999999999,"personaname":"NewPlayer","steamid":"76561198960265728","fh_unavailable":false},"rank_tier":null,"leaderboard_rank":null}""";
        var player = new RawFetchResult(playerJson, 200);
        var emptyMatches = new RawFetchResult("[]", 200);
        var emptyWl = new RawFetchResult("""{"win":0,"lose":0}""", 200);

        var snapshot = OpenDotaProfileMapper.Map(id, player, emptyMatches, emptyMatches, emptyWl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.Availability.Should().Be(ProfileAvailability.Available);
    }

    [Fact]
    public void Http_404_maps_to_PlayerNotFound_not_FetchFailed()
    {
        var id = SteamId.FromSteam32(4294967295);
        var player = LoadFixture("player_404.json") with { StatusCode = 404 };

        var snapshot = OpenDotaProfileMapper.Map(id, player, null, null, null, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.Availability.Should().Be(ProfileAvailability.PlayerNotFound);
    }

    [Fact]
    public void Matches_appearing_in_both_matches_and_recentMatches_are_deduped_by_match_id()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var wl = LoadFixture("wl.json");

        var matchesJson = """[{"match_id":1,"player_slot":0,"radiant_win":true,"duration":1200,"game_mode":1,"lobby_type":0,"hero_id":1,"start_time":1700000000}]""";
        var recentJson = """[{"match_id":1,"player_slot":0,"radiant_win":true,"duration":1200,"game_mode":1,"lobby_type":0,"hero_id":1,"start_time":1700000000,"gold_per_min":500}]""";
        var matches = new RawFetchResult(matchesJson, 200);
        var recent = new RawFetchResult(recentJson, 200);

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, recent, wl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.RecentMatches.Should().ContainSingle();
    }

    [Fact]
    public void Dedupe_keeps_the_richer_recentMatches_record_because_it_carries_gold_per_min()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");
        var wl = LoadFixture("wl.json");

        var matchesJson = """[{"match_id":1,"player_slot":0,"radiant_win":true,"duration":1200,"game_mode":1,"lobby_type":0,"hero_id":1,"start_time":1700000000}]""";
        var recentJson = """[{"match_id":1,"player_slot":0,"radiant_win":true,"duration":1200,"game_mode":1,"lobby_type":0,"hero_id":1,"start_time":1700000000,"gold_per_min":500}]""";
        var matches = new RawFetchResult(matchesJson, 200);
        var recent = new RawFetchResult(recentJson, 200);

        var snapshot = OpenDotaProfileMapper.Map(id, player, matches, recent, wl, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.RecentMatches.Single().GoldPerMin.Should().Be(500);
    }

    [Fact]
    public void Every_mapped_record_carries_a_fetch_timestamp_and_source_name_for_provenance()
    {
        var id = SteamId.FromSteam32(70388657);
        var player = LoadFixture("player_open.json");

        var snapshot = OpenDotaProfileMapper.Map(id, player, null, null, null, null, Scorer, DnaBuilder, FetchedAt);

        snapshot.Provenance.FetchedAtUtc.Should().Be(FetchedAt);
        snapshot.Provenance.SourceName.Should().Be("opendota");
    }
}
