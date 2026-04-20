using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Publishing;

/// <summary>
/// Publishes short-form video content to YouTube Shorts via YouTube Data API v3.
/// Uses resumable upload: POST https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable
/// Videos under 60s with vertical aspect ratio are automatically treated as Shorts.
/// Appending #Shorts to the title ensures Shorts classification.
/// </summary>
public class YouTubePublisher : ISocialMediaPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<YouTubePublisher> _logger;

    public YouTubePublisher(HttpClient http, ILogger<YouTubePublisher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public PlatformType SupportedPlatform => PlatformType.YouTube;

    public async Task<PublishResult> PublishPostAsync(PublishRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request.MediaUrl is null)
                return new PublishResult(false, null, null, "YouTube requires a video for publishing");

            // Step 1: Download video from the presigned MinIO URL
            var videoStream = await _http.GetStreamAsync(request.MediaUrl, ct);

            // Step 2: Build the video resource metadata
            var title = !string.IsNullOrWhiteSpace(request.Title)
                ? $"{request.Title} #Shorts"
                : "#Shorts";

            var description = request.Content ?? string.Empty;

            var videoResource = new
            {
                snippet = new
                {
                    title,
                    description,
                    categoryId = "22" // People & Blogs
                },
                status = new
                {
                    privacyStatus = "public",
                    selfDeclaredMadeForKids = false
                }
            };

            // Step 3: Initiate resumable upload
            var initRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status")
            {
                Content = JsonContent.Create(videoResource)
            };
            initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

            var initResponse = await _http.SendAsync(initRequest, ct);
            initResponse.EnsureSuccessStatusCode();

            var uploadUri = initResponse.Headers.Location?.ToString();
            if (uploadUri is null)
                return new PublishResult(false, null, null, "Failed to get YouTube upload URI");

            // Step 4: Upload video data
            using var memoryStream = new MemoryStream();
            await videoStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUri)
            {
                Content = new StreamContent(memoryStream)
            };
            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(
                request.MediaContentType ?? "video/mp4");

            var uploadResponse = await _http.SendAsync(uploadRequest, ct);
            uploadResponse.EnsureSuccessStatusCode();

            var uploadJson = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var videoId = uploadJson.GetProperty("id").GetString()!;

            return new PublishResult(
                true,
                $"https://youtube.com/shorts/{videoId}",
                videoId,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to YouTube");
            return new PublishResult(false, null, null, ex.Message);
        }
    }
}
