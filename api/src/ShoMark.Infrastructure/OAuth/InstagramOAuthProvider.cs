using System.Net.Http.Json;
using System.Text.Json;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.OAuth;

/// <summary>
/// Instagram OAuth via Facebook Graph API (Instagram Business accounts).
/// Auth endpoint: https://www.facebook.com/v21.0/dialog/oauth
/// Token endpoint: https://graph.facebook.com/v21.0/oauth/access_token
/// Long-lived token: https://graph.facebook.com/v21.0/oauth/access_token?grant_type=fb_exchange_token
/// </summary>
public class InstagramOAuthProvider : IOAuthProvider
{
    private readonly HttpClient _http;

    public InstagramOAuthProvider(HttpClient http) => _http = http;

    public PlatformType SupportedPlatform => PlatformType.Instagram;

    public string GetAuthorizationUrl(OAuthPlatformConfig config, string state)
    {
        var scopes = Uri.EscapeDataString(config.Scopes);
        return $"https://www.facebook.com/v21.0/dialog/oauth" +
               $"?client_id={config.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
               $"&scope={scopes}" +
               $"&state={state}" +
               $"&response_type=code";
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, CancellationToken ct = default)
    {
        // Exchange code for short-lived token
        var tokenUrl = $"https://graph.facebook.com/v21.0/oauth/access_token" +
                       $"?client_id={config.ClientId}" +
                       $"&client_secret={config.ClientSecret}" +
                       $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
                       $"&code={Uri.EscapeDataString(code)}";

        var tokenResponse = await _http.GetFromJsonAsync<JsonElement>(tokenUrl, ct);
        var shortLivedToken = tokenResponse.GetProperty("access_token").GetString()!;

        // Exchange for long-lived token
        var longLivedUrl = $"https://graph.facebook.com/v21.0/oauth/access_token" +
                           $"?grant_type=fb_exchange_token" +
                           $"&client_id={config.ClientId}" +
                           $"&client_secret={config.ClientSecret}" +
                           $"&fb_exchange_token={shortLivedToken}";

        var longLivedResponse = await _http.GetFromJsonAsync<JsonElement>(longLivedUrl, ct);
        var accessToken = longLivedResponse.GetProperty("access_token").GetString()!;
        var expiresIn = longLivedResponse.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;

        // Get Instagram Business account info
        var accountName = await GetAccountNameAsync(accessToken, ct);

        return new OAuthTokenResult(accessToken, null, expiresIn, accountName);
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default)
    {
        // Instagram long-lived tokens are refreshed by exchanging them again
        var url = $"https://graph.instagram.com/refresh_access_token" +
                  $"?grant_type=ig_refresh_token" +
                  $"&access_token={refreshToken}";

        var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
        var accessToken = response.GetProperty("access_token").GetString()!;
        var expiresIn = response.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;

        return new OAuthTokenResult(accessToken, null, expiresIn, null);
    }

    private async Task<string?> GetAccountNameAsync(string accessToken, CancellationToken ct)
    {
        var url = $"https://graph.facebook.com/v21.0/me/accounts?access_token={accessToken}";
        var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);

        if (response.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
        {
            var pageId = data[0].GetProperty("id").GetString()!;
            var pageToken = data[0].GetProperty("access_token").GetString()!;

            var igUrl = $"https://graph.facebook.com/v21.0/{pageId}?fields=instagram_business_account&access_token={pageToken}";
            var igResponse = await _http.GetFromJsonAsync<JsonElement>(igUrl, ct);

            if (igResponse.TryGetProperty("instagram_business_account", out var igAccount))
            {
                var igId = igAccount.GetProperty("id").GetString()!;
                var nameUrl = $"https://graph.facebook.com/v21.0/{igId}?fields=username&access_token={pageToken}";
                var nameResponse = await _http.GetFromJsonAsync<JsonElement>(nameUrl, ct);
                return nameResponse.TryGetProperty("username", out var username) ? username.GetString() : null;
            }
        }

        return null;
    }
}
