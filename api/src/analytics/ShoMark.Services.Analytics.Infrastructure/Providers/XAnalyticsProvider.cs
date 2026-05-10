using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ShoMark.Analytics.Infrastructure.Providers;

/// <summary>
/// Fetches tweet metrics from the X (Twitter) API v2.
/// Endpoint: GET /2/tweets/{id}?tweet.fields=public_metrics
/// Docs: https://developer.x.com/en/docs/twitter-api/tweets/lookup/api-reference/get-tweets-id
/// </summary>
public sealed class XAnalyticsProvider : IPlatformAnalyticsProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<XAnalyticsProvider> _logger;

    public string PlatformType => "X";

    public XAnalyticsProvider(HttpClient http, ILogger<XAnalyticsProvider> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.twitter.com");
        _logger = logger;
    }

    public async Task<PlatformMetrics> FetchAsync(string externalPostId, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/2/tweets/{externalPostId}?tweet.fields=public_metrics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<TweetResponse>(body, JsonOptions.Default)
            ?? throw new InvalidOperationException("Empty response from X API");

        var m = result.Data?.PublicMetrics;
        if (m is null)
        {
            _logger.LogDebug("No public_metrics in X response for tweet {TweetId}", externalPostId);
            return new PlatformMetrics(0, 0, 0, 0);
        }

        _logger.LogDebug("X metrics for {TweetId}: impressions={Impressions}", externalPostId, m.ImpressionCount);

        return new PlatformMetrics(
            Views: m.ImpressionCount,
            Likes: m.LikeCount,
            Shares: m.RetweetCount,
            Comments: m.ReplyCount);
    }

    private sealed record TweetResponse(
        [property: JsonPropertyName("data")] TweetData? Data);

    private sealed record TweetData(
        [property: JsonPropertyName("public_metrics")] PublicMetrics? PublicMetrics);

    private sealed record PublicMetrics(
        [property: JsonPropertyName("impression_count")] long ImpressionCount,
        [property: JsonPropertyName("like_count")] long LikeCount,
        [property: JsonPropertyName("retweet_count")] long RetweetCount,
        [property: JsonPropertyName("reply_count")] long ReplyCount);
}
