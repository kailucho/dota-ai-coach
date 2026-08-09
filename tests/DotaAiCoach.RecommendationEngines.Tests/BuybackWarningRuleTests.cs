using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class BuybackWarningRuleTests
{
    private readonly BuybackWarningRule _rule = new();
    private readonly RuleContext _context = new() { Now = DateTimeOffset.UtcNow };

    [Fact]
    public void Fires_at_P0_when_unavailable_and_late_and_dead()
    {
        var features = TestFeatures.Usable(hasBuyback: false, gameMinutes: 25, isAlive: false);

        var result = _rule.Evaluate(features, _context);

        result.Should().NotBeNull();
        result!.Priority.Should().Be(MessagePriority.P0);
    }

    [Fact]
    public void Fires_at_P0_when_unavailable_and_late_and_low_health()
    {
        var features = TestFeatures.Usable(hasBuyback: false, gameMinutes: 25, isAlive: true, healthFraction: 0.3);

        var result = _rule.Evaluate(features, _context);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Does_not_fire_when_buyback_availability_is_undecidable_null()
    {
        var features = TestFeatures.Usable(hasBuyback: null, gameMinutes: 25, isAlive: false);

        var result = _rule.Evaluate(features, _context);

        result.Should().BeNull("null means undecidable, never treat as unavailable");
    }

    [Fact]
    public void Does_not_fire_early_game_even_without_buyback()
    {
        var features = TestFeatures.Usable(hasBuyback: false, gameMinutes: 5, isAlive: false);

        var result = _rule.Evaluate(features, _context);

        result.Should().BeNull();
    }

    [Fact]
    public void Does_not_fire_at_full_health_while_alive()
    {
        var features = TestFeatures.Usable(hasBuyback: false, gameMinutes: 25, isAlive: true, healthFraction: 1.0);

        var result = _rule.Evaluate(features, _context);

        result.Should().BeNull();
    }
}
