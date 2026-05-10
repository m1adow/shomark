using System.Net.Http.Json;
using System.Text.Json;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.OAuth;

/// <summary>
/// Instagram OAuth via Instagram Login (native Instagram OAuth, not Facebook Graph API).
/// Auth endpoint:         https://www.instagram.com/oauth/authorize
/// Token endpoint (POST): https://api.instagram.com/oauth/access_token
/// Long-lived token:      https://graph.instagram.com/access_token?grant_type=ig_exchange_token
/// Refresh:               https://graph.instagram.com/refresh_access_token?grant_type=ig_refresh_token
/// </summary>
public class InstagramOAuthProvider : IOAuthProvider
{
    private readonly HttpClient _http;

    public InstagramOAuthProvider(HttpClient http) => _http = http;

    public PlatformType SupportedPlatform => PlatformType.Instagram;

    public OAuthAuthorizationResult GetAuthorizationUrl(OAuthPlatformConfig config, string state)
    {
        var scopes = Uri.EscapeDataString(config.Scopes);
        var url = $"https://www.instagram.com/oauth/authorize" +
               $"?client_id={config.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
               $"&scope={scopes}" +
               $"&state={state}" +
               $"&response_type=code";
        return new OAuthAuthorizationResult(url, null);
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, string? codeVerifier, CancellationToken ct = default)
    {
        // Exchange code for short-lived token (must be a POST with form data)
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["grant_type"]    = "authorization_code",
            ["redirect_uri"]  = config.RedirectUri,
            ["code"]          = code,
        });

        var tokenResponse = await _http.PostAsync("https://api.instagram.com/oauth/access_token", form, ct);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var shortLivedToken = tokenJson.GetProperty("access_token").GetString()!;

        // Exchange for long-lived token (60-day)
        var longLivedUrl = $"https://graph.instagram.com/access_token" +
                           $"?grant_type=ig_exchange_token" +
                           $"&client_id={config.ClientId}" +
                           $"&client_secret={config.ClientSecret}" +
                           $"&access_token={shortLivedToken}";

        var longLivedJson = await _http.GetFromJsonAsync<JsonElement>(longLivedUrl, ct);
        var accessToken = longLivedJson.GetProperty("access_token").GetString()!;
        var expiresIn = longLivedJson.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;

        var accountName = await GetUsernameAsync(accessToken, ct);

        return new OAuthTokenResult(accessToken, null, expiresIn, accountName);
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default)
    {
        // Long-lived tokens are refreshed by passing them again (not a separate refresh_token)
        var url = $"https://graph.instagram.com/refresh_access_token" +
                  $"?grant_type=ig_refresh_token" +
                  $"&access_token={refreshToken}";

        var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
        var accessToken = response.GetProperty("access_token").GetString()!;
        var expiresIn = response.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;

        return new OAuthTokenResult(accessToken, null, expiresIn, null);
    }

    private async Task<string?> GetUsernameAsync(string accessToken, CancellationToken ct)
    {
        var url = $"https://graph.instagram.com/me?fields=username&access_token={accessToken}";
        var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
        return response.TryGetProperty("username", out var username) ? username.GetString() : null;
    }
}
