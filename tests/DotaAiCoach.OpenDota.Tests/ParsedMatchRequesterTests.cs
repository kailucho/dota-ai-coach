using DotaAiCoach.OpenDota;
using FluentAssertions;

namespace DotaAiCoach.OpenDota.Tests;

public class ParsedMatchRequesterTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dota-ai-coach-parsereq-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class ScriptedClient : IOpenDotaClient
    {
        private readonly Queue<string?> _jobPollResponses;
        public int PostCallCount { get; private set; }
        public int GetCallCount { get; private set; }
        public string MatchDetailJson { get; set; } = """{"match_id":1,"players":[]}""";

        public ScriptedClient(IEnumerable<string?> jobPollResponsesInOrder)
        {
            _jobPollResponses = new Queue<string?>(jobPollResponsesInOrder);
        }

        public Task<OpenDotaResponse> PostAsync(string relativePath, CancellationToken ct)
        {
            PostCallCount++;
            return Task.FromResult(new OpenDotaResponse { RawJson = """{"job":{"jobId":42}}""", StatusCode = 200, FetchedAtUtc = DateTimeOffset.UtcNow });
        }

        public Task<OpenDotaResponse> GetAsync(string relativePath, CancellationToken ct)
        {
            GetCallCount++;
            if (relativePath.StartsWith("/request/"))
            {
                var next = _jobPollResponses.Count > 0 ? _jobPollResponses.Dequeue() : "null";
                return Task.FromResult(new OpenDotaResponse { RawJson = next, StatusCode = 200, FetchedAtUtc = DateTimeOffset.UtcNow });
            }

            return Task.FromResult(new OpenDotaResponse { RawJson = MatchDetailJson, StatusCode = 200, FetchedAtUtc = DateTimeOffset.UtcNow });
        }
    }

    [Fact]
    public async Task Deep_refresh_stops_polling_after_max_attempts_instead_of_waiting_forever_for_a_job_that_never_completes()
    {
        var dir = NewTempDir();
        var neverCompletingJob = Enumerable.Repeat<string?>("""{"id":42}""", 20);
        var client = new ScriptedClient(neverCompletingJob);
        var cache = new RawResponseCache(dir);
        var cachingClient = new CachingOpenDotaClient(client, cache, offlineOnly: false);
        var requester = new ParsedMatchRequester(client, cachingClient, maxPollAttempts: 3, pollInterval: TimeSpan.FromMilliseconds(1));

        var parsedCount = await requester.ParseMatchesAsync(new[] { 1L }, progress: null, CancellationToken.None);

        parsedCount.Should().Be(0, "the job never reported completion within the max poll attempts");
    }

    [Fact]
    public async Task Deep_refresh_never_reparses_a_match_already_cached_with_IsParsed_true()
    {
        var dir = NewTempDir();
        var client = new ScriptedClient(new string?[] { "null" });
        var cache = new RawResponseCache(dir);
        await cache.WriteAsync("/matches/1", "already-parsed-body", 200, isParsed: true, CancellationToken.None);
        var cachingClient = new CachingOpenDotaClient(client, cache, offlineOnly: false);
        var requester = new ParsedMatchRequester(client, cachingClient, maxPollAttempts: 3, pollInterval: TimeSpan.FromMilliseconds(1));

        var parsedCount = await requester.ParseMatchesAsync(new[] { 1L }, progress: null, CancellationToken.None);

        parsedCount.Should().Be(1);
        client.PostCallCount.Should().Be(0, "an already-parsed match must not trigger a new /request POST");
    }

    [Fact]
    public async Task Deep_refresh_is_bounded_to_the_configured_max_match_count_even_when_more_unparsed_matches_exist()
    {
        var dir = NewTempDir();
        var client = new ScriptedClient(Enumerable.Repeat<string?>("null", 20));
        var cache = new RawResponseCache(dir);
        var cachingClient = new CachingOpenDotaClient(client, cache, offlineOnly: false);
        var requester = new ParsedMatchRequester(client, cachingClient, maxPollAttempts: 3, pollInterval: TimeSpan.FromMilliseconds(1));

        var matchIds = Enumerable.Range(1, 3).Select(i => (long)i).ToArray();
        var parsedCount = await requester.ParseMatchesAsync(matchIds, progress: null, CancellationToken.None);

        parsedCount.Should().Be(3);
        client.PostCallCount.Should().Be(3, "only the matches actually passed in should be parsed, bounding is the caller's responsibility");
    }

    [Fact]
    public async Task A_completed_parse_result_is_cached_with_IsParsed_true()
    {
        var dir = NewTempDir();
        var client = new ScriptedClient(new string?[] { "null" }) { MatchDetailJson = """{"match_id":1,"players":[{"account_id":5}]}""" };
        var cache = new RawResponseCache(dir);
        var cachingClient = new CachingOpenDotaClient(client, cache, offlineOnly: false);
        var requester = new ParsedMatchRequester(client, cachingClient, maxPollAttempts: 3, pollInterval: TimeSpan.FromMilliseconds(1));

        await requester.ParseMatchesAsync(new[] { 1L }, progress: null, CancellationToken.None);

        var cached = await cache.ReadAsync("/matches/1", CancellationToken.None);
        cached.Should().NotBeNull();
        cached!.IsParsed.Should().BeTrue();
    }
}
