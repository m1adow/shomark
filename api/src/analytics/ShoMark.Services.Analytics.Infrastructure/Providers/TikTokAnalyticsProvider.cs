using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ShoMark.Analytics.Infrastructure.Providers;

/// <summary>
/// Fetches video metrics from the TikTok API.
/// Endpoint: POST /v2/video/query/views/
/// Docs: https://developers.tiktok.com/doc/login-kit-manage-user-access-tokens
/// </summary>
public sealed class TikTokAnalyticsProvider : IPlatformAnalyticsProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<TikTokAnalyticsProvider> _logger;

    public string PlatformType => "TikTok";

    public TikTokAnalyticsProvider(HttpClient http, ILogger<TikTokAnalyticsProvider> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://open.tiktokapis.com");
        _logger = logger;
    }

    public async Task<PlatformMetrics> FetchAsync(string externalPostId, string accessToken, CancellationToken ct)
    {
        var payload = new
        {
            filters = new { video_ids = new[] { externalPostId } },
            fields = "item_id,statistics"
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/video/query/views/")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<TikTokResponse>(body, JsonOptions.Default)
            ?? throw new InvalidOperationException("Empty response from TikTok API");

        var video = result.Data?.Videos?.FirstOrDefault();
        if (video is null)
        {
            _logger.LogDebug("No TikTok data for video {VideoId}", externalPostId);
            return new PlatformMetrics(0, 0, 0, 0);
        }

        _logger.LogDebug("TikTok metrics for {VideoId}: plays={Plays}", externalPostId, video.Statistics.PlayCount);

        return new PlatformMetrics(
            Views: video.Statistics.PlayCount,
            Likes: video.Statistics.DiggCount,
            Shares: video.Statistics.ShareCount,
            Comments: video.Statistics.CommentCount);
    }

    private sealed record TikTokResponse(
        [property: JsonPropertyName("data")] TikTokData? Data);

    private sealed record TikTokData(
        [property: JsonPropertyName("videos")] List<TikTokVideo>? Videos);

    private sealed record TikTokVideo(
        [property: JsonPropertyName("item_id")] string ItemId,
        [property: JsonPropertyName("statistics")] TikTokStatistics Statistics);

    private sealed record TikTokStatistics(
        [property: JsonPropertyName("play_count")] long PlayCount,
        [property: JsonPropertyName("digg_count")] long DiggCount,
        [property: JsonPropertyName("comment_count")] long CommentCount,
        [property: JsonPropertyName("share_count")] long ShareCount);
}
