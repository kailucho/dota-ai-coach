namespace DotaAiCoach.PlayerProfile.Tests;

/// <summary>Deterministic, manually-advanced clock for recency/patch-window tests.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
