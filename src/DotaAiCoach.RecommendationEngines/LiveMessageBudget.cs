using DotaAiCoach.Domain.Recommendations;

namespace DotaAiCoach.RecommendationEngines;

/// <summary>
/// Enforces dota-live-coach's core rule: the engine may evaluate many
/// variables, but the player should normally see only 1-3 messages.
/// Implements cooldowns (per priority tier) and global minimum spacing.
/// The MessageKey mechanism (bucketed by rule, e.g. a 200g band in
/// AffordabilityRule) is what provides the change-threshold half of
/// hysteresis — a rule re-firing with the same MessageKey within its
/// cooldown is suppressed here.
///
/// Select() is called very frequently in production (once per GSI
/// payload, often several times per second). A message that clears its
/// gates must keep being returned by every subsequent call for the
/// duration of its own cooldown window — not just the single call in
/// which it was first selected — or it would flash on screen for a
/// fraction of a second instead of staying visible. This was a real bug
/// found during live-client validation: recommendations were computed
/// correctly but visually disappeared almost instantly.
///
/// This class is a deliberate placeholder for a future standalone
/// RecommendationOrchestrator project (per dota-architecture) — its logic
/// is intentionally self-contained so it can move there wholesale once
/// multiple recommendation engines exist. See docs/ROADMAP.md.
/// </summary>
public sealed class LiveMessageBudgetOptions
{
    public int MaxMessages { get; set; } = 3;
    public TimeSpan P0Cooldown { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan P1Cooldown { get; set; } = TimeSpan.FromSeconds(45);
    public TimeSpan P2Cooldown { get; set; } = TimeSpan.FromSeconds(90);
    public TimeSpan GlobalMinimumSpacing { get; set; } = TimeSpan.FromSeconds(3);
}

public sealed class LiveMessageBudget
{
    private readonly LiveMessageBudgetOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, DateTimeOffset> _lastFiredByKey = new();

    // Tracks the currently-displayed message per key so it can keep being
    // shown for the duration of its own cooldown window, not just the
    // single Select() call in which it was first chosen.
    private readonly Dictionary<string, LiveMessage> _displayedByKey = new();
    private DateTimeOffset? _lastAnyMessageAt;

    public LiveMessageBudget(LiveMessageBudgetOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new LiveMessageBudgetOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<LiveMessage> Select(IReadOnlyList<Recommendation> candidates)
    {
        var now = _timeProvider.GetUtcNow();

        // Drop anything currently displayed whose own cooldown window has
        // fully elapsed — it's had its full time on screen and should
        // clear rather than linger indefinitely.
        foreach (var key in _displayedByKey.Keys.ToArray())
        {
            if (!_lastFiredByKey.TryGetValue(key, out var firedAt) || now - firedAt >= CooldownFor(_displayedByKey[key].Priority))
            {
                _displayedByKey.Remove(key);
            }
        }

        var candidateList = candidates.OrderBy(c => c.Priority).ThenByDescending(c => c.Score).ToArray();

        foreach (var candidate in candidateList)
        {
            if (_displayedByKey.ContainsKey(candidate.MessageKey))
            {
                // Already on screen and still within its cooldown window —
                // stays displayed without needing to re-clear the spacing/
                // budget gates below.
                continue;
            }

            if (_displayedByKey.Count >= _options.MaxMessages)
            {
                continue;
            }

            if (!ClearsCooldown(candidate, now))
            {
                continue;
            }

            if (_lastAnyMessageAt is { } lastAny && now - lastAny < _options.GlobalMinimumSpacing)
            {
                continue;
            }

            var message = new LiveMessage(candidate.RuleId, candidate.Priority, candidate.Message, candidate.MessageKey, now);
            _displayedByKey[candidate.MessageKey] = message;
            _lastFiredByKey[candidate.MessageKey] = now;
            _lastAnyMessageAt = now;
        }

        return _displayedByKey.Values
            .OrderBy(m => m.Priority)
            .Take(_options.MaxMessages)
            .ToArray();
    }

    private TimeSpan CooldownFor(MessagePriority priority) => priority switch
    {
        MessagePriority.P0 => _options.P0Cooldown,
        MessagePriority.P1 => _options.P1Cooldown,
        _ => _options.P2Cooldown
    };

    private bool ClearsCooldown(Recommendation candidate, DateTimeOffset now)
    {
        if (!_lastFiredByKey.TryGetValue(candidate.MessageKey, out var lastFired))
        {
            return true;
        }

        return now - lastFired >= CooldownFor(candidate.Priority);
    }
}
