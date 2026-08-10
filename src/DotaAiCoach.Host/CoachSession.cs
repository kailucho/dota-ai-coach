using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Domain.MlInference;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.Gsi;
using DotaAiCoach.Gsi.Listener;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;
using DotaAiCoach.Gsi.Replay;
using DotaAiCoach.MlInference;
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

        var (itemRankPredictor, itemPopularity, winProbabilityPredictor) = TryLoadMlPredictors(listenerLogger);

        _ruleEngine = new RuleRecommendationEngine(
            new IRule[]
            {
                new NextItemRule(),
                new AffordabilityRule(),
                new BuybackWarningRule(),
                new PowerSpikeRule(),
                new WinProbabilityRule()
            },
            itemRankPredictor: itemRankPredictor,
            itemPopularity: itemPopularity,
            winProbabilityPredictor: winProbabilityPredictor);
        _budget = new LiveMessageBudget();

        _pipeline.StateUpdated += OnStateUpdated;
    }

    public MatchState CurrentState => _pipeline.State;
    public FeedHealth Health { get; private set; } = FeedHealth.NeverConnectedDefault;
    public IReadOnlyList<LiveMessage> ActiveMessages { get; private set; } = Array.Empty<LiveMessage>();
    public ItemPlan CurrentItemPlan { get; private set; } = ItemPlan.Empty;

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
        // ItemPlan is read from the raw (unbudgeted) recommendations, not
        // ActiveMessages — it's a persistent panel, not subject to
        // LiveMessageBudget's cooldown/max-message gating. See
        // ICoachSession.CurrentItemPlan.
        CurrentItemPlan = recommendations.FirstOrDefault(r => r.RuleId == "next-item")?.ItemPlan ?? ItemPlan.Empty;

        Updated?.Invoke(this, new CoachUpdate(_pipeline.State, Health, events, ActiveMessages, CurrentItemPlan));
    }

    public void SetAverageRankTier(int? rankTier) => _ruleEngine.SetAverageRankTier(rankTier);

    public async ValueTask DisposeAsync()
    {
        _healthTimer?.Dispose();
        await _listener.DisposeAsync();
    }

    /// <summary>
    /// Loads the bundled ONNX models and popularity table once per
    /// session. Deliberately tolerant of a missing/corrupt model
    /// directory (logs and returns nulls) rather than crashing session
    /// construction — RuleRecommendationEngine and NextItemSelector both
    /// fall back to their non-ML behavior when these are null, so a
    /// packaging problem degrades the live coach instead of preventing
    /// it from starting.
    /// </summary>
    private static (IItemRankPredictor? ItemRankPredictor, IHeroItemPopularityLookup? ItemPopularity, IWinProbabilityPredictor? WinProbabilityPredictor)
        TryLoadMlPredictors(ILogger logger)
    {
        IItemRankPredictor? itemRankPredictor = null;
        IHeroItemPopularityLookup? itemPopularity = null;
        IWinProbabilityPredictor? winProbabilityPredictor = null;

        try
        {
            itemRankPredictor = new ItemRankPredictor();
            itemPopularity = HeroItemPopularityTable.LoadFromBundledModels();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Item-ranker ML model failed to load; falling back to the non-ML item progression.");
        }

        try
        {
            winProbabilityPredictor = new WinProbabilityPredictor();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Win-probability ML model failed to load; the win-probability live message will not appear.");
        }

        return (itemRankPredictor, itemPopularity, winProbabilityPredictor);
    }
}
