using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class AffordabilityRuleTests
{
    private readonly AffordabilityRule _rule = new();
    private readonly RuleContext _context = new() { Now = DateTimeOffset.UtcNow };

    // item_boots costs 500 (see ItemCatalog).

    [Fact]
    public void Affordable_band_fires_when_gold_meets_cost()
    {
        var features = TestFeatures.Usable(gold: 500);

        var result = _rule.Evaluate(features, _context);

        result!.ReasonCodes.Should().Contain(ReasonCode.AffordableNow);
    }

    [Fact]
    public void Close_band_fires_at_75_percent_of_cost()
    {
        var features = TestFeatures.Usable(gold: 375); // exactly 75% of 500

        var result = _rule.Evaluate(features, _context);

        result!.ReasonCodes.Should().Contain(ReasonCode.CloseToAffording);
    }

    [Fact]
    public void Farm_band_fires_below_75_percent_with_known_gpm()
    {
        var features = TestFeatures.Usable(gold: 100, gpm: 300);

        var result = _rule.Evaluate(features, _context);

        result!.ReasonCodes.Should().Contain(ReasonCode.FarmUntilAffordable);
        result.Message.Should().Contain("Farm");
    }

    [Fact]
    public void Returns_null_when_far_short_and_gpm_unknown_no_fabricated_eta()
    {
        var features = TestFeatures.Usable(gold: 50, gpm: null);

        var result = _rule.Evaluate(features, _context);

        result.Should().BeNull();
    }
}
