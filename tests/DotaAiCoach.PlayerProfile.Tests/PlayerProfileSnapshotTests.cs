using DotaAiCoach.PlayerProfile.Model;
using FluentAssertions;

namespace DotaAiCoach.PlayerProfile.Tests;

public class PlayerProfileSnapshotTests
{
    [Fact]
    public void Empty_snapshot_has_empty_collections_not_nulls()
    {
        var snapshot = PlayerProfileSnapshot.Empty;

        snapshot.Heroes.Should().NotBeNull().And.BeEmpty();
        snapshot.RecentMatches.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Availability_defaults_to_Unknown_not_Available()
    {
        var snapshot = PlayerProfileSnapshot.Empty;

        snapshot.Availability.Should().Be(ProfileAvailability.Unknown);
    }
}
