using System.Collections;
using System.Reflection;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.PlayerProfile.Model;
using DotaAiCoach.RecommendationEngines;
using FluentAssertions;

namespace DotaAiCoach.FairPlay.Tests;

/// <summary>
/// Guards the Fase 2 scope decision that player profile data does NOT
/// feed live recommendations in this phase (see docs/adr/0012 and
/// docs/ROADMAP.md). Deliberately a separate file from
/// LiveRecommendationInputTests/FairPlayClassificationTests rather than
/// extending RelevantAssemblies() there: broadening "no live type is
/// Forbidden" into "no type anywhere is Forbidden" would silently break
/// once a future phase adds real PostgameOnly types.
/// </summary>
public class ProfileFairPlayTests
{
    [Fact]
    public void No_PlayerProfile_type_is_reachable_from_LiveFeatureSet_because_profile_does_not_feed_live_recommendations_in_this_phase()
    {
        var visited = new HashSet<Type>();
        Walk(typeof(LiveFeatureSet), visited);

        var profileTypes = visited.Where(t => t.Namespace is not null && t.Namespace.StartsWith("DotaAiCoach.PlayerProfile", StringComparison.Ordinal)).ToArray();

        profileTypes.Should().BeEmpty(
            "player profile data must not reach the live recommendation input in this phase — see docs/adr/0012");
    }

    [Fact]
    public void PlayerProfileSnapshot_is_classified_PublicPreflight_with_ExternalApi_source()
    {
        var attribute = typeof(PlayerProfileSnapshot).GetCustomAttribute<FairPlaySourceAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Source.Should().Be(DataSourceKind.ExternalApi);
        attribute.Classification.Should().Be(FairPlayClassification.PublicPreflight);
    }

    [Fact]
    public void PublicPreflight_remains_in_LiveAllowedSet_so_a_future_phase_can_feed_profile_data_to_draft_recommendations()
    {
        FairPlayPolicy.IsAllowedLive(FairPlayClassification.PublicPreflight).Should().BeTrue();
    }

    [Fact]
    public void ParsedMatchDetail_is_not_reachable_from_LiveFeatureSet_because_it_only_feeds_DotaDnaProfile_not_live_recommendations()
    {
        // ParsedMatchDetail (docs/adr/0016) is the same PublicPreflight/
        // ExternalApi OpenDota data as the rest of Fase 2, just richer —
        // it flows into DotaDnaBuilder.Build and the resulting scores end
        // up on PlayerProfileSnapshot.Dna, never on LiveFeatureSet.
        var visited = new HashSet<Type>();
        Walk(typeof(LiveFeatureSet), visited);

        visited.Should().NotContain(typeof(PlayerProfile.Model.ParsedMatchDetail));
    }

    [Fact]
    public void Every_classified_type_in_PlayerProfile_documents_visibility_and_justification()
    {
        var types = typeof(PlayerProfileSnapshot).Assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<FairPlaySourceAttribute>() is not null);

        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<FairPlaySourceAttribute>()!;
            attribute.Visibility.Should().NotBeNullOrWhiteSpace($"{type.FullName} must document Visibility");
            attribute.Justification.Should().NotBeNullOrWhiteSpace($"{type.FullName} must document Justification");
        }
    }

    // Local copy of LiveRecommendationInputTests' walker: same algorithm,
    // deliberately duplicated rather than shared, since these two test
    // classes assert different (and independently evolvable) claims.
    private static void Walk(Type type, HashSet<Type> visited)
    {
        if (type.IsGenericParameter || !visited.Add(type))
        {
            return;
        }

        if (IsBclOrPrimitive(type))
        {
            return;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                Walk(arg, visited);
            }
            return;
        }

        if (type.Namespace is null || !type.Namespace.StartsWith("DotaAiCoach", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Walk(property.PropertyType, visited);
        }
    }

    private static bool IsBclOrPrimitive(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
            type == typeof(Guid) || type == typeof(void))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return IsBclOrPrimitive(Nullable.GetUnderlyingType(type)!);
        }

        return type.Namespace is not null && type.Namespace.StartsWith("System", StringComparison.Ordinal);
    }
}
