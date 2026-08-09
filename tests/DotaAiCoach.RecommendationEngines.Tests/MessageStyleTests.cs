using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

/// <summary>
/// Makes dota-live-coach's message-style rule (short, imperative, no
/// explanatory clause) mechanically checkable rather than only a
/// stylistic review concern.
/// </summary>
public class MessageStyleTests
{
    private static readonly RuleContext Context = new() { Now = DateTimeOffset.UtcNow };

    public static IEnumerable<object[]> RuleAndFeatureMatrix()
    {
        yield return new object[] { new NextItemRule(), TestFeatures.Usable(gold: 0) };
        yield return new object[] { new AffordabilityRule(), TestFeatures.Usable(gold: 500) };
        yield return new object[] { new AffordabilityRule(), TestFeatures.Usable(gold: 375) };
        yield return new object[] { new AffordabilityRule(), TestFeatures.Usable(gold: 100, gpm: 300) };
        yield return new object[] { new BuybackWarningRule(), TestFeatures.Usable(hasBuyback: false, gameMinutes: 25, isAlive: false) };
    }

    [Theory]
    [MemberData(nameof(RuleAndFeatureMatrix))]
    public void Message_is_short_and_has_no_explanatory_clause(IRule rule, DotaAiCoach.FeatureEngine.LiveFeatureSet features)
    {
        var result = rule.Evaluate(features, Context);

        result.Should().NotBeNull();
        result!.Message.Length.Should().BeLessThanOrEqualTo(28,
            $"live messages must be glanceable at a glance, but got '{result.Message}' ({result.Message.Length} chars)");
        result.Message.Should().NotContain(" because ", "live messages state the fact/action, not the justification");
        result.Message.Should().EndWith(".");
    }
}
