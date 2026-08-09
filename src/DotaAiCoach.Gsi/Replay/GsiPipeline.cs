using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Gsi.Normalization;
using DotaAiCoach.Gsi.Parsing;

namespace DotaAiCoach.Gsi.Replay;

/// <summary>
/// The default GSI ingestion sink: owns the current MatchState, parses and
/// merges incoming payloads, and exposes proactive staleness evaluation.
/// Used by both the live listener (DotaAiCoach.Gsi.Listener) and the
/// replay simulator's in-process target, so both paths exercise identical
/// pipeline behavior.
/// </summary>
public sealed class GsiPipeline
{
    private readonly IGsiPayloadParser _parser;
    private readonly GsiNormalizer _normalizer;

    public GsiPipeline(IGsiPayloadParser parser, GsiNormalizer normalizer)
    {
        _parser = parser;
        _normalizer = normalizer;
    }

    public MatchState State { get; } = new();

    public event EventHandler<NormalizationResult>? StateUpdated;

    /// <summary>Parses and merges a raw JSON payload. Malformed payloads are dropped (logged by the caller), never throw.</summary>
    public NormalizationResult? Ingest(string rawJson, DateTimeOffset receivedAt)
    {
        var parseResult = _parser.Parse(rawJson);
        if (!parseResult.Success || parseResult.Payload is null)
        {
            return null;
        }

        var result = _normalizer.Merge(State, parseResult.Payload, receivedAt);
        StateUpdated?.Invoke(this, result);
        return result;
    }

    /// <summary>Called on a timer by the host so staleness is detected proactively, independent of payload arrival.</summary>
    public FeedHealth EvaluateHealth(DateTimeOffset now) => _normalizer.EvaluateHealth(State, now);
}
