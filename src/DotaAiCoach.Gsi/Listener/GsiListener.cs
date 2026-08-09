using System.Text.Json;
using DotaAiCoach.Gsi.Replay;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotaAiCoach.Gsi.Listener;

/// <summary>
/// Self-hosted Kestrel HTTP listener for the GSI POST endpoint, bound to
/// 127.0.0.1 only (loopback — the game client and this app run on the
/// same machine; no external exposure is needed or wanted). Responds 200
/// as fast as possible and defers all parsing/merging to
/// <see cref="GsiPipeline"/> — a slow response makes the Dota client back
/// off (per the GSI .cfg timeout setting), so speed here is a functional
/// requirement, not an optimization.
/// </summary>
public sealed class GsiListener : IAsyncDisposable
{
    private readonly GsiOptions _options;
    private readonly GsiPipeline _pipeline;
    private readonly ILogger<GsiListener> _logger;
    private WebApplication? _app;

    public GsiListener(GsiOptions options, GsiPipeline pipeline, ILogger<GsiListener> logger)
    {
        _options = options;
        _pipeline = pipeline;
        _logger = logger;
    }

    /// <summary>Resolved bound port. Useful when GsiOptions.Port is 0 (ephemeral port, used by tests).</summary>
    public int BoundPort { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(kestrel =>
        {
            kestrel.Listen(System.Net.IPAddress.Loopback, _options.Port);
        });
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapPost(_options.Route, async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(request.HttpContext.RequestAborted);

            if (!IsAuthorized(body))
            {
                _logger.LogWarning("Dropped GSI payload with missing/incorrect auth token.");
                return Results.Ok();
            }

            _pipeline.Ingest(body, DateTimeOffset.UtcNow);
            return Results.Ok();
        });

        await _app.StartAsync(ct);

        var server = _app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        var addressFeature = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
        var address = addressFeature?.Addresses.FirstOrDefault();
        BoundPort = address is not null ? new Uri(address).Port : _options.Port;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_app is not null)
        {
            await _app.StopAsync(ct);
        }
    }

    private bool IsAuthorized(string rawJson)
    {
        if (string.IsNullOrEmpty(_options.AuthToken))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("auth", out var auth) &&
                auth.TryGetProperty("token", out var token))
            {
                return token.GetString() == _options.AuthToken;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
