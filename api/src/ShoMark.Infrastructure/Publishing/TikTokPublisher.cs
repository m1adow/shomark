using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Publishing;

/// <summary>
/// Publishes video content to TikTok via Content Posting API.
/// Flow: 1) Init upload  2) Upload video  3) Publish
/// Docs: https://developers.tiktok.com/doc/content-posting-api-get-started
/// </summary>
public class TikTokPublisher : ISocialMediaPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<TikTokPublisher> _logger;

    public TikTokPublisher(HttpClient http, ILogger<TikTokPublisher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public PlatformType SupportedPlatform => PlatformType.TikTok;

    public async Task<PublishResult> PublishPostAsync(PublishRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request.MediaUrl is null)
                return new PublishResult(false, null, null, "TikTok requires a video URL for publishing");

            // Step 1: Initialize video upload via URL pull
            var initRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://open.tiktokapis.com/v2/post/publish/video/init/")
            {
                Content = JsonContent.Create(new
                {
                    post_info = new
                    {
                        title = BuildCaption(request),
                        privacy_level = "SELF_ONLY", // Default to self-only; can be changed
                        disable_duet = false,
                        disable_stitch = false,
                        disable_comment = false
                    },
                    source_info = new
                    {
                        source = "PULL_FROM_URL",
                        video_url = request.MediaUrl
                    }
                })
            };
            initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

            var initResponse = await _http.SendAsync(initRequest, ct);
            initResponse.EnsureSuccessStatusCode();
            var initJson = await initResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

            var publishId = initJson.TryGetProperty("data", out var data)
                            && data.TryGetProperty("publish_id", out var pid)
                ? pid.GetString()
                : null;

            if (publishId is null)
                return new PublishResult(false, null, null, "Failed to initialize TikTok video upload");

            // Step 2: Poll for publish status
            var externalId = await WaitForPublishAsync(publishId, request.AccessToken, ct);

            return new PublishResult(true, null, externalId ?? publishId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to TikTok");
            return new PublishResult(false, null, null, ex.Message);
        }
    }

    private async Task<string?> WaitForPublishAsync(string publishId, string accessToken, CancellationToken ct)
    {
        for (var i = 0; i < 30; i++)
        {
            var statusRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://open.tiktokapis.com/v2/post/publish/status/fetch/")
            {
                Content = JsonContent.Create(new { publish_id = publishId })
            };
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var statusResponse = await _http.SendAsync(statusRequest, ct);
            if (!statusResponse.IsSuccessStatusCode)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                continue;
            }

            var json = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (json.TryGetProperty("data", out var data) &&
                data.TryGetProperty("status", out var status))
            {
                var statusStr = status.GetString();
                if (statusStr == "PUBLISH_COMPLETE")
                    return data.TryGetProperty("publicaly_available_post_id", out var postId)
                        ? postId.GetString()
                        : null;

                if (statusStr == "FAILED")
                    return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }

        return null;
    }

    private static string BuildCaption(PublishRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title)) parts.Add(request.Title);
        if (!string.IsNullOrWhiteSpace(request.Content)) parts.Add(request.Content);
        return string.Join(" ", parts);
    }
}
