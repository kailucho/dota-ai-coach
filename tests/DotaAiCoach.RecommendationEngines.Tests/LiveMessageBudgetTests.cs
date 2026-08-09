using DotaAiCoach.Domain.Recommendations;
using FluentAssertions;

namespace DotaAiCoach.RecommendationEngines.Tests;

public class LiveMessageBudgetTests
{
    private static Recommendation Rec(string ruleId, MessagePriority priority, string key, double score = 0.5) => new()
    {
        RuleId = ruleId,
        Priority = priority,
        Message = "Test.",
        MessageKey = key,
        Score = score
    };

    [Fact]
    public void Caps_at_max_messages()
    {
        var options = new LiveMessageBudgetOptions { MaxMessages = 3, GlobalMinimumSpacing = TimeSpan.Zero };
        var budget = new LiveMessageBudget(options, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var candidates = new[]
        {
            Rec("a", MessagePriority.P1, "a"),
            Rec("b", MessagePriority.P1, "b"),
            Rec("c", MessagePriority.P1, "c"),
            Rec("d", MessagePriority.P1, "d"),
            Rec("e", MessagePriority.P1, "e")
        };

        var selected = budget.Select(candidates);

        selected.Should().HaveCount(3);
    }

    [Fact]
    public void P0_takes_precedence_over_lower_tiers_when_budget_is_full()
    {
        var options = new LiveMessageBudgetOptions { MaxMessages = 1, GlobalMinimumSpacing = TimeSpan.Zero };
        var budget = new LiveMessageBudget(options, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var candidates = new[]
        {
            Rec("info", MessagePriority.P2, "info"),
            Rec("critical", MessagePriority.P0, "critical")
        };

        var selected = budget.Select(candidates);

        selected.Should().ContainSingle().Which.RuleId.Should().Be("critical");
    }

    [Fact]
    public void Same_message_key_stays_displayed_through_its_cooldown_then_may_refresh_after()
    {
        // Select() is called very frequently in production (once per GSI
        // payload, often multiple times per second) — a message must stay
        // visible for the duration of its own cooldown window, not just
        // the single call in which it was first selected, or it would
        // flash on screen for a fraction of a second. This was a real bug
        // found against a live Dota client: recommendations were computed
        // correctly but visually disappeared almost instantly.
        var options = new LiveMessageBudgetOptions
        {
            P1Cooldown = TimeSpan.FromSeconds(45),
            GlobalMinimumSpacing = TimeSpan.Zero
        };
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var budget = new LiveMessageBudget(options, clock);

        var candidate = Rec("next-item", MessagePriority.P1, "next-item:item_boots");

        budget.Select(new[] { candidate }).Should().ContainSingle();

        // Simulate rapid re-evaluation (as happens once per GSI payload)
        // while still well within the cooldown window — the message must
        // remain displayed, not disappear.
        clock.Advance(TimeSpan.FromMilliseconds(300));
        budget.Select(new[] { candidate }).Should().ContainSingle("the message must still be on screen a fraction of a second after first appearing");

        clock.Advance(TimeSpan.FromSeconds(10));
        budget.Select(new[] { candidate }).Should().ContainSingle("still within the 45s P1 cooldown, so it stays displayed rather than disappearing");

        // Once the cooldown fully elapses, the message clears; a fresh
        // candidate with the same key is then treated as never-fired again.
        clock.Advance(TimeSpan.FromSeconds(40));
        budget.Select(Array.Empty<Recommendation>()).Should().BeEmpty("the cooldown window has fully elapsed with no new candidate, so the stale message clears");

        budget.Select(new[] { candidate }).Should().ContainSingle("cooldown has elapsed and the message can reappear");
    }

    [Fact]
    public void Gold_drift_sequence_changes_displayed_message_at_most_a_couple_of_times()
    {
        // Simulates 30 rapid updates (once per second, well inside the 45s
        // P1 cooldown) as gold rises from 1200 to 1600 (200g band =>
        // MessageKey changes roughly every 200g, i.e. twice across this
        // range). The message stays continuously selected throughout (per
        // the fix above), so the meaningful anti-spam assertion is on how
        // many times its actual DISPLAYED CONTENT changed — not on the
        // raw per-call selected count, which is expected to be high now
        // that a still-valid message stays selected across every call.
        var options = new LiveMessageBudgetOptions { P1Cooldown = TimeSpan.FromSeconds(45), GlobalMinimumSpacing = TimeSpan.Zero };
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var budget = new LiveMessageBudget(options, clock);

        var distinctKeysShown = new HashSet<string>();
        for (var i = 0; i < 30; i++)
        {
            var gold = 1200 + i * (400 / 30);
            var remaining = 2250 - gold; // item_blink costs 2250
            var bucket = remaining / 200;
            var candidate = Rec("affordability", MessagePriority.P1, $"afford:item_blink:close:{bucket}");

            var selected = budget.Select(new[] { candidate });
            // A bucket transition can briefly show both the old (still
            // within its own cooldown) and the new message at once — that's
            // correct anti-spam behavior (each key has its own cooldown),
            // not spam. The candidate itself must always appear somewhere
            // in the selection, though.
            selected.Should().Contain(m => m.MessageKey == candidate.MessageKey);
            foreach (var m in selected)
            {
                distinctKeysShown.Add(m.MessageKey);
            }

            clock.Advance(TimeSpan.FromSeconds(1));
        }

        distinctKeysShown.Should().HaveCountLessThanOrEqualTo(3,
            "the 200g bucket should change content only a couple of times across a 400g drift");
    }
}
