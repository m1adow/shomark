using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Publishing;

/// <summary>
/// Publishes video content to TikTok via Content Posting API (FILE_UPLOAD).
/// PULL_FROM_URL requires verified domain ownership — not suitable for internal MinIO URLs.
/// Flow: 1) Download video from presigned URL  2) Init FILE_UPLOAD  3) Upload chunks  4) Poll status
/// Docs: https://developers.tiktok.com/doc/content-posting-api-media-transfer-guide
/// </summary>
public class TikTokPublisher : ISocialMediaPublisher
{
    private const int ChunkSize = 10 * 1024 * 1024; // 10 MB per chunk

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

            // Step 1: Download the video into memory (from MinIO presigned URL)
            _logger.LogInformation("Downloading video for TikTok from presigned URL");
            var videoBytes = await _http.GetByteArrayAsync(request.MediaUrl, ct);
            var videoSize = videoBytes.Length;
            // chunk_size must match the size of every chunk except the last.
            // Use the lesser of ChunkSize and videoSize so a single-chunk upload is valid.
            var actualChunkSize = Math.Min(ChunkSize, videoSize);
            var totalChunks = (int)Math.Ceiling((double)videoSize / actualChunkSize);

            _logger.LogInformation("Video downloaded: {Size} bytes, {Chunks} chunks of {ChunkSize} bytes",
                videoSize, totalChunks, actualChunkSize);

            // Step 2: Init FILE_UPLOAD
            var initBody = JsonSerializer.Serialize(new
            {
                post_info = new
                {
                    title = BuildCaption(request),
                    privacy_level = "SELF_ONLY",
                    disable_duet = false,
                    disable_stitch = false,
                    disable_comment = false,
                    video_cover_timestamp_ms = 1000
                },
                source_info = new
                {
                    source = "FILE_UPLOAD",
                    video_size = videoSize,
                    chunk_size = actualChunkSize,
                    total_chunk_count = totalChunks
                }
            });

            var initRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://open.tiktokapis.com/v2/post/publish/video/init/")
            {
                Content = new StringContent(initBody, System.Text.Encoding.UTF8, "application/json")
            };
            initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

            var initResponse = await _http.SendAsync(initRequest, ct);
            if (!initResponse.IsSuccessStatusCode)
            {
                var errorBody = await initResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("TikTok init failed ({Status}): {Body}", (int)initResponse.StatusCode, errorBody);

                // Provide actionable messages for known TikTok sandbox restrictions
                string userMessage;
                if (errorBody.Contains("unaudited_client_can_only_post_to_private_accounts"))
                    userMessage = "TikTok publishing failed: your developer app is unaudited. " +
                                  "To test, set your TikTok account to Private (TikTok app → Profile → Settings → Privacy → Private Account). " +
                                  "For production, submit your app for audit at developers.tiktok.com.";
                else if (errorBody.Contains("url_ownership_unverified"))
                    userMessage = "TikTok publishing failed: the video URL domain is not verified. Using FILE_UPLOAD should avoid this — check the publisher configuration.";
                else
                    userMessage = $"TikTok init failed ({(int)initResponse.StatusCode}): {errorBody}";

                return new PublishResult(false, null, null, userMessage);
            }

            var initJson = await initResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

            string? publishId = null;
            string? uploadUrl = null;
            if (initJson.TryGetProperty("data", out var initData))
            {
                initData.TryGetProperty("publish_id", out var pid);
                publishId = pid.GetString();
                initData.TryGetProperty("upload_url", out var uurl);
                uploadUrl = uurl.GetString();
            }

            if (publishId is null || uploadUrl is null)
                return new PublishResult(false, null, null, "TikTok init did not return publish_id or upload_url");

            // Step 3: Upload chunks
            for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                var offset = chunkIndex * actualChunkSize;
                var length = Math.Min(actualChunkSize, videoSize - offset);
                var rangeEnd = offset + length - 1;

                var chunkRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
                {
                    Content = new ByteArrayContent(videoBytes, offset, length)
                };
                chunkRequest.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
                chunkRequest.Content.Headers.ContentLength = length;
                chunkRequest.Headers.Add("Content-Range", $"bytes {offset}-{rangeEnd}/{videoSize}");

                var chunkResponse = await _http.SendAsync(chunkRequest, ct);
                if (!chunkResponse.IsSuccessStatusCode)
                {
                    var chunkError = await chunkResponse.Content.ReadAsStringAsync(ct);
                    throw new InvalidOperationException(
                        $"TikTok chunk {chunkIndex} upload failed ({(int)chunkResponse.StatusCode}): {chunkError}");
                }

                _logger.LogDebug("TikTok chunk {Index}/{Total} uploaded", chunkIndex + 1, totalChunks);
            }

            // Step 4: Poll for publish status
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
            var statusBody = System.Text.Json.JsonSerializer.Serialize(new { publish_id = publishId });
            var statusRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://open.tiktokapis.com/v2/post/publish/status/fetch/")
            {
                Content = new StringContent(statusBody, System.Text.Encoding.UTF8, "application/json")
            };
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var statusResponse = await _http.SendAsync(statusRequest, ct);
            if (!statusResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok status check failed ({Status}), retrying…", (int)statusResponse.StatusCode);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                continue;
            }

            var json = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (json.TryGetProperty("data", out var data) &&
                data.TryGetProperty("status", out var status))
            {
                var statusStr = status.GetString();
                _logger.LogDebug("TikTok publish_id {PublishId} status: {Status} (attempt {Attempt}/30)",
                    publishId, statusStr, i + 1);

                if (statusStr == "PUBLISH_COMPLETE")
                    return data.TryGetProperty("publicly_available_post_id", out var postId)
                        ? postId.GetString()
                        : publishId;

                if (statusStr == "FAILED")
                {
                    var failReason = data.TryGetProperty("fail_reason", out var fr) ? fr.GetString() : "unknown";
                    throw new InvalidOperationException($"TikTok publish failed: {failReason}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }

        throw new TimeoutException("TikTok video did not finish publishing within 90 seconds.");
    }

    private static string BuildCaption(PublishRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title)) parts.Add(request.Title);
        if (!string.IsNullOrWhiteSpace(request.Content)) parts.Add(request.Content);
        return string.Join(" ", parts);
    }
}
