using DotaAiCoach.Domain.Events;
using DotaAiCoach.RecommendationEngines.Rules;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class PowerSpikeRuleTests
{
    private readonly PowerSpikeRule _rule = new();
    private readonly RuleContext _context = new() { Now = DateTimeOffset.UtcNow };

    [Fact]
    public void Fires_on_level_6_event()
    {
        var events = new DomainEvent[] { new HeroLeveledUp(5, 6, 400, DateTimeOffset.UtcNow) };
        var features = TestFeatures.Usable(recentEvents: events, heroLevel: 6);

        var result = _rule.Evaluate(features, _context);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Fires_on_bkb_purchase_event()
    {
        var events = new DomainEvent[]
        {
            new ItemPurchased("item_black_king_bar", ItemLocation.Inventory, 1500, DateTimeOffset.UtcNow)
        };
        var features = TestFeatures.Usable(recentEvents: events);

        var result = _rule.Evaluate(features, _context);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Does_not_fire_on_an_unrelated_tick()
    {
        var events = new DomainEvent[] { new HeroLeveledUp(4, 5, 350, DateTimeOffset.UtcNow) };
        var features = TestFeatures.Usable(recentEvents: events, heroLevel: 5);

        var result = _rule.Evaluate(features, _context);

        result.Should().BeNull();
    }
}
