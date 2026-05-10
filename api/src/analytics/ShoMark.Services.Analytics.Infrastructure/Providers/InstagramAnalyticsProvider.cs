using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ShoMark.Analytics.Infrastructure.Providers;

/// <summary>
/// Fetches post metrics from the Instagram Graph API.
/// Endpoint: GET /v19.0/{media-id}/insights?metric=impressions,reach,likes,comments,shares
/// Docs: https://developers.facebook.com/docs/instagram-api/reference/ig-media/insights
/// </summary>
public sealed class InstagramAnalyticsProvider : IPlatformAnalyticsProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<InstagramAnalyticsProvider> _logger;

    public string PlatformType => "Instagram";

    public InstagramAnalyticsProvider(HttpClient http, ILogger<InstagramAnalyticsProvider> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://graph.facebook.com");
        _logger = logger;
    }

    public async Task<PlatformMetrics> FetchAsync(string externalPostId, string accessToken, CancellationToken ct)
    {
        var url = $"/v19.0/{externalPostId}/insights?metric=impressions,reach,likes,comments,shares&access_token={accessToken}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<InsightsResponse>(body, JsonOptions.Default)
            ?? throw new InvalidOperationException("Empty response from Instagram Insights API");

        var metrics = result.Data.ToDictionary(d => d.Name, d => d.Values.FirstOrDefault()?.Value ?? 0L);

        _logger.LogDebug("Instagram metrics for {PostId}: {Metrics}", externalPostId, metrics);

        return new PlatformMetrics(
            Views: metrics.GetValueOrDefault("impressions"),
            Likes: metrics.GetValueOrDefault("likes"),
            Shares: metrics.GetValueOrDefault("shares"),
            Comments: metrics.GetValueOrDefault("comments"));
    }

    private sealed record InsightsResponse(
        [property: JsonPropertyName("data")] List<InsightItem> Data);

    private sealed record InsightItem(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("values")] List<InsightValue> Values);

    private sealed record InsightValue(
        [property: JsonPropertyName("value")] long Value);
}
