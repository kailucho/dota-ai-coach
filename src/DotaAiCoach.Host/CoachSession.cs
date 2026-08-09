using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Listener;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using DotaAiCoach.Gsi.Replay;
using DotaAiCoach.RecommendationEngines;
using DotaAiCoach.RecommendationEngines.Rules;
using Microsoft.Extensions.Logging;

namespace DotaAiCoach.Host;

/// <summary>
/// Composes the full live pipeline: GSI listener -> GsiPipeline ->
/// FeatureEngine -> RuleRecommendationEngine -> LiveMessageBudget, and
/// exposes only the ICoachSession surface to consumers (the overlay).
/// A periodic timer evaluates feed health independent of payload arrival,
/// per dota-gsi's proactive-staleness-detection requirement.
/// </summary>
public sealed class CoachSession : ICoachSession, IAsyncDisposable
{
    private readonly GsiListener _listener;
    private readonly GsiPipeline _pipeline;
    private readonly IFeatureEngine _featureEngine;
    private readonly RuleRecommendationEngine _ruleEngine;
    private readonly LiveMessageBudget _budget;
    private Timer? _healthTimer;

    public CoachSession(GsiOptions options, ILogger<GsiListener> listenerLogger)
    {
        var parser = new GsiPayloadParser();
        var normalizer = new GsiNormalizer(options);
        _pipeline = new GsiPipeline(parser, normalizer);
        _listener = new GsiListener(options, _pipeline, listenerLogger);

        _featureEngine = new FeatureEngine.FeatureEngine();
        _ruleEngine = new RuleRecommendationEngine(new IRule[]
        {
            new NextItemRule(),
            new AffordabilityRule(),
            new BuybackWarningRule(),
            new PowerSpikeRule()
        });
        _budget = new LiveMessageBudget();

        _pipeline.StateUpdated += OnStateUpdated;
    }

    public MatchState CurrentState => _pipeline.State;
    public FeedHealth Health { get; private set; } = FeedHealth.NeverConnectedDefault;
    public IReadOnlyList<LiveMessage> ActiveMessages { get; private set; } = Array.Empty<LiveMessage>();

    public event EventHandler<CoachUpdate>? Updated;

    public async Task StartAsync(CancellationToken ct)
    {
        await _listener.StartAsync(ct);
        _healthTimer = new Timer(_ => EvaluateHealthAndNotify(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _healthTimer?.Dispose();
        await _listener.StopAsync(ct);
    }

    private void OnStateUpdated(object? sender, NormalizationResult result)
    {
        Health = result.Health;
        Recompute(result.Events);
    }

    private void EvaluateHealthAndNotify()
    {
        Health = _pipeline.EvaluateHealth(DateTimeOffset.UtcNow);
        Recompute(Array.Empty<DomainEvent>());
    }

    private void Recompute(IReadOnlyList<DomainEvent> events)
    {
        var features = _featureEngine.Build(_pipeline.State, Health, events);
        var recommendations = _ruleEngine.Evaluate(features);
        ActiveMessages = _budget.Select(recommendations);

        Updated?.Invoke(this, new CoachUpdate(_pipeline.State, Health, events, ActiveMessages));
    }

    public async ValueTask DisposeAsync()
    {
        _healthTimer?.Dispose();
        await _listener.DisposeAsync();
    }
}
