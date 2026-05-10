using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ShoMark.Analytics.Infrastructure.Providers;

/// <summary>
/// Fetches video metrics from the YouTube Analytics API.
/// Endpoint: GET /v2/reports?ids=channel==MINE&amp;metrics=views,likes,comments,shares&amp;filters=video=={videoId}
/// Docs: https://developers.google.com/youtube/analytics/reference/reports/query
/// </summary>
public sealed class YouTubeAnalyticsProvider : IPlatformAnalyticsProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<YouTubeAnalyticsProvider> _logger;

    public string PlatformType => "YouTube";

    public YouTubeAnalyticsProvider(HttpClient http, ILogger<YouTubeAnalyticsProvider> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://youtubeanalytics.googleapis.com");
        _logger = logger;
    }

    public async Task<PlatformMetrics> FetchAsync(string externalPostId, string accessToken, CancellationToken ct)
    {
        var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");

        var url = $"/v2/reports?ids=channel==MINE&metrics=views,likes,comments,shares" +
                  $"&filters=video=={externalPostId}&startDate={startDate}&endDate={endDate}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ReportsResponse>(body, JsonOptions.Default)
            ?? throw new InvalidOperationException("Empty response from YouTube Analytics API");

        if (result.Rows is null || result.Rows.Count == 0)
        {
            _logger.LogDebug("No YouTube Analytics rows for video {VideoId}", externalPostId);
            return new PlatformMetrics(0, 0, 0, 0);
        }

        // columnHeaders order: views, likes, comments, shares
        var row = result.Rows[0];
        _logger.LogDebug("YouTube metrics for {VideoId}: views={Views}", externalPostId, row[0]);

        return new PlatformMetrics(
            Views: (long)row[0],
            Likes: (long)row[1],
            Shares: (long)row[3],
            Comments: (long)row[2]);
    }

    private sealed record ReportsResponse(
        [property: JsonPropertyName("rows")] List<List<double>>? Rows);
}
