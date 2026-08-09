using System.Reflection;
using DotaAiCoach.FairPlay;
using FluentAssertions;

namespace DotaAiCoach.FairPlay.Tests;

/// <summary>
/// Mechanically enforces dota-fairplay's testing requirement: every type
/// carrying a FairPlaySourceAttribute has a live-allowed classification
/// (nothing is Forbidden/SpectatorOnly in this slice, since everything is
/// player-owned GSI data), and every attribute fills in the required
/// five-field documentation (Visibility/Justification non-empty).
/// </summary>
public class FairPlayClassificationTests
{
    private static IEnumerable<(Type Type, FairPlaySourceAttribute Attribute)> ClassifiedTypes(Assembly assembly) =>
        assembly.GetExportedTypes()
            .Select(t => (Type: t, Attribute: t.GetCustomAttribute<FairPlaySourceAttribute>()))
            .Where(x => x.Attribute is not null)
            .Select(x => (x.Type, x.Attribute!));

    private static IEnumerable<Assembly> RelevantAssemblies() => new[]
    {
        typeof(Domain.Match.MatchState).Assembly,
        typeof(FeatureEngine.LiveFeatureSet).Assembly
    };

    [Fact]
    public void No_classified_type_is_Forbidden_or_SpectatorOnly()
    {
        foreach (var assembly in RelevantAssemblies())
        {
            foreach (var (type, attribute) in ClassifiedTypes(assembly))
            {
                attribute.Classification.Should().NotBe(FairPlayClassification.Forbidden,
                    $"{type.Name} must never be classified Forbidden in this project");
                attribute.Classification.Should().NotBe(FairPlayClassification.SpectatorOnly,
                    $"{type.Name} must never be classified SpectatorOnly for a live-facing type");
            }
        }
    }

    [Fact]
    public void Every_classified_type_has_non_empty_visibility_and_justification()
    {
        foreach (var assembly in RelevantAssemblies())
        {
            foreach (var (type, attribute) in ClassifiedTypes(assembly))
            {
                attribute.Visibility.Should().NotBeNullOrWhiteSpace(
                    $"{type.Name}'s FairPlaySource must document a concrete visibility, per the five-field template");
                attribute.Justification.Should().NotBeNullOrWhiteSpace(
                    $"{type.Name}'s FairPlaySource must document a justification");
            }
        }
    }

    [Fact]
    public void Domain_and_FeatureEngine_have_at_least_one_classified_type_each()
    {
        // Guards against the test suite silently checking nothing because
        // no types were ever attributed.
        ClassifiedTypes(typeof(Domain.Match.MatchState).Assembly).Should().NotBeEmpty();
        ClassifiedTypes(typeof(FeatureEngine.LiveFeatureSet).Assembly).Should().NotBeEmpty();
    }
}
