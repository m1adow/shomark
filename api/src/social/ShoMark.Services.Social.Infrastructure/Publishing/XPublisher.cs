using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Publishing;

/// <summary>
/// Publishes content to X (Twitter) via X API v2.
/// Text-only: POST https://api.x.com/2/tweets
/// With media: 1) Upload media via media/upload  2) Create tweet with media_ids
/// Docs: https://developer.x.com/en/docs/x-api/tweets/manage-tweets/api-reference/post-tweets
/// </summary>
public class XPublisher : ISocialMediaPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<XPublisher> _logger;

    public XPublisher(HttpClient http, ILogger<XPublisher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public PlatformType SupportedPlatform => PlatformType.X;

    public async Task<PublishResult> PublishPostAsync(PublishRequest request, CancellationToken ct = default)
    {
        try
        {
            var text = BuildTweetText(request);
            string? mediaId = null;

            // Upload media if provided
            if (request.MediaUrl is not null)
            {
                mediaId = await UploadMediaAsync(request, ct);
            }

            // Create tweet
            var tweetPayload = new Dictionary<string, object>
            {
                ["text"] = text
            };

            if (mediaId is not null)
            {
                tweetPayload["media"] = new { media_ids = new[] { mediaId } };
            }

            var tweetRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.x.com/2/tweets")
            {
                Content = JsonContent.Create(tweetPayload)
            };
            tweetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

            var tweetResponse = await _http.SendAsync(tweetRequest, ct);
            tweetResponse.EnsureSuccessStatusCode();
            var tweetJson = await tweetResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

            var tweetId = tweetJson.GetProperty("data").GetProperty("id").GetString()!;

            // Get the username to construct URL
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.x.com/2/users/me");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);
            var userResponse = await _http.SendAsync(userRequest, ct);
            string? username = null;

            if (userResponse.IsSuccessStatusCode)
            {
                var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                username = userJson.TryGetProperty("data", out var userData) &&
                           userData.TryGetProperty("username", out var un)
                    ? un.GetString()
                    : null;
            }

            var externalUrl = username is not null
                ? $"https://x.com/{username}/status/{tweetId}"
                : $"https://x.com/i/status/{tweetId}";

            return new PublishResult(true, externalUrl, tweetId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to X");
            return new PublishResult(false, null, null, ex.Message);
        }
    }

    private async Task<string?> UploadMediaAsync(PublishRequest request, CancellationToken ct)
    {
        // X media upload uses v1.1 chunked upload endpoint
        // Step 1: INIT
        var initPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["command"] = "INIT",
            ["media_type"] = request.MediaContentType ?? "video/mp4",
            ["media_category"] = "tweet_video"
        });

        var initRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://upload.twitter.com/1.1/media/upload.json")
        {
            Content = initPayload
        };
        initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var initResponse = await _http.SendAsync(initRequest, ct);
        if (!initResponse.IsSuccessStatusCode) return null;

        var initJson = await initResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var mediaIdStr = initJson.GetProperty("media_id_string").GetString()!;

        // Step 2: APPEND — download video and upload in chunks
        var videoStream = await _http.GetStreamAsync(request.MediaUrl!, ct);
        using var memoryStream = new MemoryStream();
        await videoStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        var segmentIndex = 0;
        var buffer = new byte[5 * 1024 * 1024]; // 5MB chunks
        int bytesRead;

        while ((bytesRead = await memoryStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            var appendContent = new MultipartFormDataContent
            {
                { new StringContent("APPEND"), "command" },
                { new StringContent(mediaIdStr), "media_id" },
                { new StringContent(segmentIndex.ToString()), "segment_index" },
                { new ByteArrayContent(buffer, 0, bytesRead), "media_data", "chunk" }
            };

            var appendRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://upload.twitter.com/1.1/media/upload.json")
            {
                Content = appendContent
            };
            appendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

            var appendResponse = await _http.SendAsync(appendRequest, ct);
            appendResponse.EnsureSuccessStatusCode();
            segmentIndex++;
        }

        // Step 3: FINALIZE
        var finalizePayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["command"] = "FINALIZE",
            ["media_id"] = mediaIdStr
        });

        var finalizeRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://upload.twitter.com/1.1/media/upload.json")
        {
            Content = finalizePayload
        };
        finalizeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var finalizeResponse = await _http.SendAsync(finalizeRequest, ct);
        finalizeResponse.EnsureSuccessStatusCode();

        // Wait for processing if needed
        var finalizeJson = await finalizeResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (finalizeJson.TryGetProperty("processing_info", out _))
        {
            await WaitForMediaProcessingAsync(mediaIdStr, request.AccessToken, ct);
        }

        return mediaIdStr;
    }

    private async Task WaitForMediaProcessingAsync(string mediaId, string accessToken, CancellationToken ct)
    {
        for (var i = 0; i < 30; i++)
        {
            var statusRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://upload.twitter.com/1.1/media/upload.json?command=STATUS&media_id={mediaId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var statusResponse = await _http.SendAsync(statusRequest, ct);
            if (!statusResponse.IsSuccessStatusCode) break;

            var json = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (json.TryGetProperty("processing_info", out var info))
            {
                var state = info.GetProperty("state").GetString();
                if (state == "succeeded") return;
                if (state == "failed") throw new InvalidOperationException("X media processing failed");
            }
            else
            {
                return; // No processing_info means it's ready
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
    }

    private static string BuildTweetText(PublishRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title)) parts.Add(request.Title);
        if (!string.IsNullOrWhiteSpace(request.Content)) parts.Add(request.Content);
        var text = string.Join("\n\n", parts);
        return text.Length > 280 ? text[..277] + "..." : text;
    }
}
