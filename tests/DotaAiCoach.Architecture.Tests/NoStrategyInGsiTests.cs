using FluentAssertions;

namespace DotaAiCoach.Architecture.Tests;

/// <summary>
/// Cheap, real check that the GSI layer stays limited to producing
/// normalized state/events, per dota-architecture's "GSI must not contain
/// recommendation logic" rule and dota-gsi's "GSI is infrastructure, not
/// recommendation logic" principle.
/// </summary>
public class NoStrategyInGsiTests
{
    private static readonly string[] StrategyVerbs = { "Recommend", "Score", "Suggest", "Rule" };

    [Fact]
    public void No_public_type_in_Gsi_has_a_strategic_sounding_name()
    {
        var gsiAssembly = typeof(global::DotaAiCoach.Gsi.Parsing.GsiPayloadParser).Assembly;

        var offendingTypes = gsiAssembly.GetExportedTypes()
            .Where(t => StrategyVerbs.Any(verb => t.Name.Contains(verb, StringComparison.Ordinal)))
            .ToArray();

        offendingTypes.Should().BeEmpty(
            "GSI is infrastructure — it should never contain types named like strategic decisions");
    }

    [Fact]
    public void Gsi_assembly_does_not_reference_RecommendationEngines()
    {
        var gsiAssembly = typeof(global::DotaAiCoach.Gsi.Parsing.GsiPayloadParser).Assembly;

        var refs = gsiAssembly.GetReferencedAssemblies().Select(a => a.Name);

        refs.Should().NotContain("DotaAiCoach.RecommendationEngines");
    }
}
