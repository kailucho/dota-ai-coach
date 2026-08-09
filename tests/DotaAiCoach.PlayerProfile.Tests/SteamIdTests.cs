using DotaAiCoach.PlayerProfile.Identity;
using FluentAssertions;

namespace DotaAiCoach.PlayerProfile.Tests;

public class SteamIdTests
{
    [Fact]
    public void Steam64_is_converted_to_steam32_by_subtracting_the_universe_offset()
    {
        SteamId.TryParse("76561198047111232", out var id).Should().BeTrue();

        id.Steam32.Should().Be(86845504);
    }

    [Fact]
    public void Steam32_input_is_accepted_unchanged_because_users_paste_either_form()
    {
        SteamId.TryParse("86845504", out var id).Should().BeTrue();

        id.Steam32.Should().Be(86845504);
    }

    [Fact]
    public void Value_below_the_steam64_base_but_implausibly_large_is_rejected_rather_than_silently_mangled()
    {
        // Larger than any plausible Steam32 friend code but still below
        // the Steam64 base — must not be treated as a valid Steam32.
        SteamId.TryParse("9999999999", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    [InlineData("0")]
    public void Empty_or_non_numeric_input_returns_false_and_does_not_throw(string? input)
    {
        var act = () => SteamId.TryParse(input, out _);

        act.Should().NotThrow();
        SteamId.TryParse(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Round_trip_steam32_to_steam64_and_back_is_lossless()
    {
        var id = SteamId.FromSteam32(86845504);

        SteamId.TryParse(id.Steam64.ToString(), out var reparsed).Should().BeTrue();
        reparsed.Steam32.Should().Be(id.Steam32);
    }
}
