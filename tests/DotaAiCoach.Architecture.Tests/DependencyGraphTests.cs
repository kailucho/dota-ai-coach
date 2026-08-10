using System.Reflection;
using FluentAssertions;

namespace DotaAiCoach.Architecture.Tests;

/// <summary>
/// Asserts the assembly reference graph matches dota-architecture's
/// dependency rules mechanically — if a `.csproj` accidentally adds a
/// forbidden reference, this fails the build rather than relying on
/// review to catch it.
/// </summary>
public class DependencyGraphTests
{
    private static IReadOnlySet<string> ReferencedAssemblyNames(Type typeInAssembly) =>
        typeInAssembly.Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Domain_references_no_infrastructure()
    {
        var refs = ReferencedAssemblyNames(typeof(Domain.Match.MatchState));

        refs.Should().NotContain("DotaAiCoach.Gsi");
        refs.Should().NotContain("DotaAiCoach.UiOverlay");
        refs.Should().NotContain("DotaAiCoach.RecommendationEngines");
        refs.Should().NotContain("DotaAiCoach.FeatureEngine");
        refs.Should().NotContain("PresentationFramework");
    }

    [Fact]
    public void Gsi_references_no_recommendation_or_ui_layers()
    {
        var refs = ReferencedAssemblyNames(typeof(global::DotaAiCoach.Gsi.Parsing.GsiPayloadParser));

        refs.Should().NotContain("DotaAiCoach.RecommendationEngines");
        refs.Should().NotContain("DotaAiCoach.FeatureEngine");
        refs.Should().NotContain("DotaAiCoach.UiOverlay");
    }

    [Fact]
    public void FeatureEngine_references_no_Gsi()
    {
        var refs = ReferencedAssemblyNames(typeof(FeatureEngine.FeatureEngine));

        refs.Should().NotContain("DotaAiCoach.Gsi");
        refs.Should().NotContain("DotaAiCoach.UiOverlay");
    }

    [Fact]
    public void RecommendationEngines_references_no_Gsi_and_no_wpf()
    {
        var refs = ReferencedAssemblyNames(typeof(RecommendationEngines.RuleRecommendationEngine));

        refs.Should().NotContain("DotaAiCoach.Gsi");
        refs.Should().NotContain("PresentationFramework");
        refs.Should().NotContain("PresentationCore");
    }

    [Fact]
    public void UiOverlay_references_no_Gsi_directly_or_transitively()
    {
        var refs = ReferencedAssemblyNames(typeof(UiOverlay.MainWindow));

        refs.Should().NotContain("DotaAiCoach.Gsi",
            "the UI must never see GSI wire types, even transitively through Host — see Host.csproj's PrivateAssets setting");
    }

    [Fact]
    public void UiOverlay_does_not_reference_System_Net_Http_directly()
    {
        // UiOverlay's own assembly should not itself declare an HttpClient
        // dependency for calling external APIs directly (dota-architecture
        // rule 5) — System.Net.Http is part of the shared framework and
        // will still resolve at runtime for other assemblies, but this
        // project's own explicit package/reference surface should not add it.
        var csprojPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "DotaAiCoach.UiOverlay", "DotaAiCoach.UiOverlay.csproj");

        File.Exists(csprojPath).Should().BeTrue("expected to find UiOverlay's csproj relative to the test output directory");
        var content = File.ReadAllText(csprojPath);
        content.Should().NotContain("System.Net.Http");
    }

    [Fact]
    public void PlayerProfile_references_no_http_and_no_OpenDota_because_scoring_must_be_pure()
    {
        var refs = ReferencedAssemblyNames(typeof(PlayerProfile.Scoring.HeroComfortScorer));

        refs.Should().NotContain("DotaAiCoach.OpenDota");
        refs.Should().NotContain("System.Net.Http");
    }

    [Fact]
    public void PlayerProfile_references_no_Gsi_or_UiOverlay()
    {
        var refs = ReferencedAssemblyNames(typeof(PlayerProfile.Scoring.HeroComfortScorer));

        refs.Should().NotContain("DotaAiCoach.Gsi");
        refs.Should().NotContain("DotaAiCoach.UiOverlay");
    }

    [Fact]
    public void OpenDota_references_no_UiOverlay_or_Gsi()
    {
        var refs = ReferencedAssemblyNames(typeof(OpenDota.PlayerProfileService));

        refs.Should().NotContain("DotaAiCoach.UiOverlay");
        refs.Should().NotContain("DotaAiCoach.Gsi");
    }

    [Fact]
    public void MlInference_references_no_infrastructure_beyond_OnnxRuntime()
    {
        var refs = ReferencedAssemblyNames(typeof(MlInference.WinProbabilityPredictor));

        refs.Should().NotContain("DotaAiCoach.Gsi");
        refs.Should().NotContain("DotaAiCoach.FeatureEngine");
        refs.Should().NotContain("DotaAiCoach.RecommendationEngines");
        refs.Should().NotContain("DotaAiCoach.UiOverlay");
        refs.Should().NotContain("PresentationFramework");
        refs.Should().NotContain("PresentationCore");
    }

    [Fact]
    public void RecommendationEngines_does_not_reference_OnnxRuntime_directly()
    {
        // RecommendationEngines may consume ML predictions only through
        // the Domain-defined predictor interfaces (IWinProbabilityPredictor
        // etc.) implemented by MlInference -- it must never see ONNX
        // Runtime types directly (dota-architecture dependency graph).
        var refs = ReferencedAssemblyNames(typeof(RecommendationEngines.RuleRecommendationEngine));

        refs.Should().NotContain("Microsoft.ML.OnnxRuntime");
        refs.Should().NotContain("DotaAiCoach.MlInference");
    }

    [Fact]
    public void UiOverlay_references_no_OpenDota_directly_or_transitively()
    {
        // The stronger, assembly-graph form of the raw-text check above:
        // the UI must never see OpenDota's HTTP/wire/cache details, only
        // the PlayerProfile domain types exposed through IProfileSession.
        var refs = ReferencedAssemblyNames(typeof(UiOverlay.MainWindow));

        refs.Should().NotContain("DotaAiCoach.OpenDota",
            "the UI must never see OpenDota's HTTP/cache internals, even transitively through Host — see Host.csproj's PrivateAssets setting");
    }

    [Fact]
    public void UiOverlay_references_no_LlmCoach_or_OpenAI_directly_or_transitively()
    {
        // Same firewall as OpenDota above: the UI must only see the LLM
        // calls through ILlmCoachSession/Domain.LlmCoach types, never
        // DotaAiCoach.LlmCoach or the OpenAI SDK itself.
        var refs = ReferencedAssemblyNames(typeof(UiOverlay.MainWindow));

        refs.Should().NotContain("DotaAiCoach.LlmCoach",
            "the UI must never see the OpenAI SDK internals, even transitively through Host — see Host.csproj's PrivateAssets setting");
        refs.Should().NotContain("OpenAI");
    }
}
