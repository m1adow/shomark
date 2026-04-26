using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Publishing;

/// <summary>
/// Publishes content to Instagram via Instagram Login (graph.instagram.com).
/// Flow: 1) Resolve IG user ID from /me  2) Create media container
///       3) Poll container until FINISHED  4) Publish container
/// Docs: https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/content-publishing
/// </summary>
public class InstagramPublisher : ISocialMediaPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<InstagramPublisher> _logger;

    public InstagramPublisher(HttpClient http, ILogger<InstagramPublisher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public PlatformType SupportedPlatform => PlatformType.Instagram;

    public async Task<PublishResult> PublishPostAsync(PublishRequest request, CancellationToken ct = default)
    {
        try
        {
            // Resolve the Instagram user ID from the access token
            var igUserId = await GetInstagramUserIdAsync(request.AccessToken, ct);
            if (igUserId is null)
                return new PublishResult(false, null, null, "Could not resolve Instagram user ID");

            // Step 1: Create media container
            string containerId;
            if (request.MediaUrl is not null && request.MediaContentType?.StartsWith("video") == true)
                containerId = await CreateReelContainerAsync(igUserId, request, ct);
            else
                containerId = await CreateImageContainerAsync(igUserId, request, ct);

            // Step 2: Wait for container to be ready (poll status)
            await WaitForContainerReadyAsync(containerId, request.AccessToken, ct);

            // Step 3: Publish the container
            var publishUrl = $"https://graph.instagram.com/v21.0/{igUserId}/media_publish" +
                             $"?creation_id={containerId}" +
                             $"&access_token={request.AccessToken}";

            var publishResponse = await _http.PostAsync(publishUrl, null, ct);
            publishResponse.EnsureSuccessStatusCode();
            var publishJson = await publishResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var mediaId = publishJson.GetProperty("id").GetString()!;

            var permalink = $"https://www.instagram.com/p/{mediaId}/";
            return new PublishResult(true, permalink, mediaId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to Instagram");
            return new PublishResult(false, null, null, ex.Message);
        }
    }

    private async Task<string?> GetInstagramUserIdAsync(string accessToken, CancellationToken ct)
    {
        var url = $"https://graph.instagram.com/me?fields=id&access_token={accessToken}";
        var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
        return response.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private async Task<string> CreateReelContainerAsync(string igUserId, PublishRequest request, CancellationToken ct)
    {
        var caption = BuildCaption(request);
        var url = $"https://graph.instagram.com/v21.0/{igUserId}/media" +
                  $"?media_type=REELS" +
                  $"&video_url={Uri.EscapeDataString(request.MediaUrl!)}" +
                  $"&caption={Uri.EscapeDataString(caption)}" +
                  $"&access_token={request.AccessToken}";

        var response = await _http.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("id").GetString()!;
    }

    private async Task<string> CreateImageContainerAsync(string igUserId, PublishRequest request, CancellationToken ct)
    {
        var caption = BuildCaption(request);
        var url = $"https://graph.instagram.com/v21.0/{igUserId}/media" +
                  $"?image_url={Uri.EscapeDataString(request.MediaUrl ?? "")}" +
                  $"&caption={Uri.EscapeDataString(caption)}" +
                  $"&access_token={request.AccessToken}";

        var response = await _http.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("id").GetString()!;
    }

    private async Task WaitForContainerReadyAsync(string containerId, string accessToken, CancellationToken ct)
    {
        for (var i = 0; i < 30; i++)
        {
            var statusUrl = $"https://graph.instagram.com/v21.0/{containerId}?fields=status_code&access_token={accessToken}";
            var statusResponse = await _http.GetFromJsonAsync<JsonElement>(statusUrl, ct);

            if (statusResponse.TryGetProperty("status_code", out var statusCode))
            {
                var status = statusCode.GetString();
                if (status == "FINISHED") return;
                if (status == "ERROR") throw new InvalidOperationException("Instagram media container processing failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        throw new TimeoutException("Instagram media container did not become ready within timeout");
    }

    private static string BuildCaption(PublishRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title)) parts.Add(request.Title);
        if (!string.IsNullOrWhiteSpace(request.Content)) parts.Add(request.Content);
        return string.Join("\n\n", parts);
    }
}
