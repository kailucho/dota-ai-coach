using System.Net.Http.Headers;
using System.Text;

namespace DotaAiCoach.Gsi.Replay;

/// <summary>
/// Drives a real, running <see cref="Listener.GsiListener"/> over HTTP —
/// the one path that proves the listener itself (Kestrel binding, request
/// handling, auth checking) works end to end, not just the in-process
/// pipeline. Observes results via the same <see cref="GsiPipeline"/>
/// instance the listener writes into, since the HTTP response itself
/// carries no state back.
/// </summary>
public sealed class HttpReplayTarget : IReplayTarget
{
    private readonly HttpClient _client;
    private readonly string _endpointUri;
    private readonly GsiPipeline _observedPipeline;

    public HttpReplayTarget(HttpClient client, string endpointUri, GsiPipeline observedPipeline)
    {
        _client = client;
        _endpointUri = endpointUri;
        _observedPipeline = observedPipeline;
    }

    public async Task<ReplayStep> SendAsync(FixturePayload payload, DateTimeOffset simulatedTime, CancellationToken ct)
    {
        using var content = new StringContent(payload.RawJson, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var previousUpdateCount = _observedPipeline.State.UpdateCount;

        var response = await _client.PostAsync(_endpointUri, content, ct);
        response.EnsureSuccessStatusCode();

        // The listener processes synchronously before returning 200, so by
        // the time PostAsync completes the pipeline has already observed
        // this payload — but guard with a short poll in case that ever
        // changes to fire-and-forget.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (_observedPipeline.State.UpdateCount == previousUpdateCount && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5, ct);
        }

        var health = _observedPipeline.EvaluateHealth(simulatedTime);
        return new ReplayStep(payload, _observedPipeline.State.Clone(), Array.Empty<Domain.Events.DomainEvent>(), health, simulatedTime);
    }
}
