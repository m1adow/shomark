using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Publishing;

/// <summary>
/// Publishes content to Instagram via Facebook Graph API (Reels / Single Media).
/// Flow: 1) Create media container  2) Poll for ready  3) Publish container
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
            // Get Instagram business account ID
            var igAccountId = await GetInstagramAccountIdAsync(request.AccessToken, ct);
            if (igAccountId is null)
                return new PublishResult(false, null, null, "Could not resolve Instagram business account ID");

            // Step 1: Create media container
            string containerId;
            if (request.MediaUrl is not null && request.MediaContentType?.StartsWith("video") == true)
            {
                // Reel (video)
                containerId = await CreateReelContainerAsync(igAccountId, request, ct);
            }
            else
            {
                // Single image post
                containerId = await CreateImageContainerAsync(igAccountId, request, ct);
            }

            // Step 2: Wait for container to be ready (poll status)
            await WaitForContainerReadyAsync(containerId, request.AccessToken, ct);

            // Step 3: Publish the container
            var publishUrl = $"https://graph.facebook.com/v21.0/{igAccountId}/media_publish" +
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

    private async Task<string?> GetInstagramAccountIdAsync(string accessToken, CancellationToken ct)
    {
        var url = $"https://graph.facebook.com/v21.0/me/accounts?access_token={accessToken}";
        var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);

        if (response.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
        {
            var pageId = data[0].GetProperty("id").GetString()!;
            var igUrl = $"https://graph.facebook.com/v21.0/{pageId}?fields=instagram_business_account&access_token={accessToken}";
            var igResponse = await _http.GetFromJsonAsync<JsonElement>(igUrl, ct);

            if (igResponse.TryGetProperty("instagram_business_account", out var igAccount))
                return igAccount.GetProperty("id").GetString();
        }
        return null;
    }

    private async Task<string> CreateReelContainerAsync(string igAccountId, PublishRequest request, CancellationToken ct)
    {
        var caption = BuildCaption(request);
        var url = $"https://graph.facebook.com/v21.0/{igAccountId}/media" +
                  $"?media_type=REELS" +
                  $"&video_url={Uri.EscapeDataString(request.MediaUrl!)}" +
                  $"&caption={Uri.EscapeDataString(caption)}" +
                  $"&access_token={request.AccessToken}";

        var response = await _http.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("id").GetString()!;
    }

    private async Task<string> CreateImageContainerAsync(string igAccountId, PublishRequest request, CancellationToken ct)
    {
        var caption = BuildCaption(request);
        var url = $"https://graph.facebook.com/v21.0/{igAccountId}/media" +
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
            var statusUrl = $"https://graph.facebook.com/v21.0/{containerId}?fields=status_code&access_token={accessToken}";
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
