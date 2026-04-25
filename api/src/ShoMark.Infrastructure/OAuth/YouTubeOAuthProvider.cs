using System.Net.Http.Json;
using System.Text.Json;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.OAuth;

/// <summary>
/// YouTube OAuth via Google OAuth 2.0.
/// Auth endpoint: https://accounts.google.com/o/oauth2/v2/auth
/// Token endpoint: https://oauth2.googleapis.com/token
/// Scopes: youtube.upload, youtube.readonly
/// </summary>
public class YouTubeOAuthProvider : IOAuthProvider
{
    private readonly HttpClient _http;

    public YouTubeOAuthProvider(HttpClient http) => _http = http;

    public PlatformType SupportedPlatform => PlatformType.YouTube;

    public string GetAuthorizationUrl(OAuthPlatformConfig config, string state)
    {
        var scopes = Uri.EscapeDataString(config.Scopes);
        return $"https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={config.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
               $"&scope={scopes}" +
               $"&state={state}" +
               $"&response_type=code" +
               $"&access_type=offline" +
               $"&prompt=consent";
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, CancellationToken ct = default)
    {
        var payload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = config.RedirectUri
        });

        var response = await _http.PostAsync("https://oauth2.googleapis.com/token", payload, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = json.GetProperty("access_token").GetString()!;
        var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        var accountName = await GetChannelNameAsync(accessToken, ct);

        return new OAuthTokenResult(accessToken, refreshToken, expiresIn, accountName);
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default)
    {
        var payload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await _http.PostAsync("https://oauth2.googleapis.com/token", payload, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = json.GetProperty("access_token").GetString()!;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        return new OAuthTokenResult(accessToken, refreshToken, expiresIn, null);
    }

    private async Task<string?> GetChannelNameAsync(string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (json.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
        {
            return items[0].GetProperty("snippet").GetProperty("title").GetString();
        }

        return null;
    }
}
