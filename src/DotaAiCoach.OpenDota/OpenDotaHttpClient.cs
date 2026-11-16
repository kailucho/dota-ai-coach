namespace DotaAiCoach.OpenDota;

/// <summary>The only file in this project (and this project the only one in the whole solution outside Gsi/GsiCapture) that touches HttpClient directly.</summary>
public sealed class OpenDotaHttpClient : IOpenDotaClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly OpenDotaOptions _options;
    private readonly RateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public OpenDotaHttpClient(OpenDotaOptions options, RateLimiter rateLimiter, TimeProvider? timeProvider = null)
    {
        _options = options;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _http = new HttpClient { Timeout = options.RequestTimeout };
    }

    public Task<OpenDotaResponse> GetAsync(string relativePath, CancellationToken ct) =>
        SendAsync(relativePath, HttpMethod.Get, ct);

    public Task<OpenDotaResponse> PostAsync(string relativePath, CancellationToken ct) =>
        SendAsync(relativePath, HttpMethod.Post, ct);

    private async Task<OpenDotaResponse> SendAsync(string relativePath, HttpMethod method, CancellationToken ct)
    {
        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var url = BuildUrl(relativePath);

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new OpenDotaResponse { FetchedAtUtc = _timeProvider.GetUtcNow(), StatusCode = null, RawJson = null };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Request timeout, not caller cancellation.
            return new OpenDotaResponse { FetchedAtUtc = _timeProvider.GetUtcNow(), StatusCode = null, RawJson = null };
        }

        var remainingMinute = TryGetHeaderInt(response, "x-rate-limit-remaining-minute");
        var remainingDay = TryGetHeaderInt(response, "x-rate-limit-remaining-day");
        _rateLimiter.ReportRemaining(remainingMinute);

        var body = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound
            ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
            : null;

        return new OpenDotaResponse
        {
            RawJson = body,
            StatusCode = (int)response.StatusCode,
            FetchedAtUtc = _timeProvider.GetUtcNow(),
            ServedFromCache = false,
            RateLimitRemainingMinute = remainingMinute,
            RateLimitRemainingDay = remainingDay,
        };
    }

    private string BuildUrl(string relativePath)
    {
        // An absolute URL (e.g. Valve's dota2.com datafeed for the current
        // patch letter, which OpenDota's /constants/patch omits) is used
        // verbatim — no base URL prefix, no OpenDota api_key appended.
        if (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        var separator = relativePath.Contains('?') ? "&" : "?";
        var apiKeySuffix = string.IsNullOrEmpty(_options.ApiKey) ? "" : $"{separator}api_key={_options.ApiKey}";
        return $"{_options.BaseUrl}{relativePath}{apiKeySuffix}";
    }

    private static int? TryGetHeaderInt(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) &&
        int.TryParse(values.FirstOrDefault(), out var parsed)
            ? parsed
            : null;

    public void Dispose() => _http.Dispose();
}
