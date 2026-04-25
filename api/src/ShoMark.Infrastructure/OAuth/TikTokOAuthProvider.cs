using System.Net.Http.Json;
using System.Text.Json;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.OAuth;

/// <summary>
/// TikTok OAuth via Login Kit.
/// Auth endpoint: https://www.tiktok.com/v2/auth/authorize/
/// Token endpoint: https://open.tiktokapis.com/v2/oauth/token/
/// </summary>
public class TikTokOAuthProvider : IOAuthProvider
{
    private readonly HttpClient _http;

    public TikTokOAuthProvider(HttpClient http) => _http = http;

    public PlatformType SupportedPlatform => PlatformType.TikTok;

    public string GetAuthorizationUrl(OAuthPlatformConfig config, string state)
    {
        var scopes = Uri.EscapeDataString(config.Scopes);
        return $"https://www.tiktok.com/v2/auth/authorize/" +
               $"?client_key={config.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
               $"&scope={scopes}" +
               $"&state={state}" +
               $"&response_type=code";
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, CancellationToken ct = default)
    {
        var payload = new
        {
            client_key = config.ClientId,
            client_secret = config.ClientSecret,
            code,
            grant_type = "authorization_code",
            redirect_uri = config.RedirectUri
        };

        var response = await _http.PostAsJsonAsync("https://open.tiktokapis.com/v2/oauth/token/", payload, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = json.GetProperty("access_token").GetString()!;
        var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;
        var openId = json.TryGetProperty("open_id", out var oid) ? oid.GetString() : null;

        var accountName = await GetAccountNameAsync(accessToken, ct) ?? openId;

        return new OAuthTokenResult(accessToken, refreshToken, expiresIn, accountName);
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default)
    {
        var payload = new
        {
            client_key = config.ClientId,
            client_secret = config.ClientSecret,
            grant_type = "refresh_token",
            refresh_token = refreshToken
        };

        var response = await _http.PostAsJsonAsync("https://open.tiktokapis.com/v2/oauth/token/", payload, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = json.GetProperty("access_token").GetString()!;
        var newRefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;

        return new OAuthTokenResult(accessToken, newRefreshToken, expiresIn, null);
    }

    private async Task<string?> GetAccountNameAsync(string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://open.tiktokapis.com/v2/user/info/?fields=display_name");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (json.TryGetProperty("data", out var data) &&
            data.TryGetProperty("user", out var user) &&
            user.TryGetProperty("display_name", out var name))
        {
            return name.GetString();
        }

        return null;
    }
}
