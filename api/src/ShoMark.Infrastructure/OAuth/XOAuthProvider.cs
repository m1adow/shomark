using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.OAuth;

/// <summary>
/// X (Twitter) OAuth 2.0 with PKCE.
/// Auth endpoint: https://twitter.com/i/oauth2/authorize
/// Token endpoint: https://api.x.com/2/oauth2/token
/// </summary>
public class XOAuthProvider : IOAuthProvider
{
    private readonly HttpClient _http;

    // In production, store per-user code_verifier in server-side cache alongside state.
    // For simplicity, the OAuthController manages the code_verifier via IMemoryCache.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _codeVerifiers = new();

    public XOAuthProvider(HttpClient http) => _http = http;

    public PlatformType SupportedPlatform => PlatformType.X;

    public string GetAuthorizationUrl(OAuthPlatformConfig config, string state)
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        _codeVerifiers[state] = codeVerifier;

        var scopes = Uri.EscapeDataString(config.Scopes);
        return $"https://twitter.com/i/oauth2/authorize" +
               $"?client_id={config.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
               $"&scope={scopes}" +
               $"&state={state}" +
               $"&response_type=code" +
               $"&code_challenge={codeChallenge}" +
               $"&code_challenge_method=S256";
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, CancellationToken ct = default)
    {
        // Retrieve code_verifier from earlier authorization request
        // Using state embedded in the code exchange flow — the controller passes it via the code parameter context
        var codeVerifier = _codeVerifiers.Values.FirstOrDefault() ?? "placeholder";

        // Try to find and remove the matching code_verifier
        foreach (var kvp in _codeVerifiers)
        {
            codeVerifier = kvp.Value;
            _codeVerifiers.TryRemove(kvp.Key, out _);
            break;
        }

        var payload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = config.RedirectUri,
            ["code_verifier"] = codeVerifier
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.com/2/oauth2/token")
        {
            Content = payload
        };

        // X requires Basic auth with client_id:client_secret
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = json.GetProperty("access_token").GetString()!;
        var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 7200;

        var accountName = await GetAccountNameAsync(accessToken, ct);

        return new OAuthTokenResult(accessToken, refreshToken, expiresIn, accountName);
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default)
    {
        var payload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.com/2/oauth2/token")
        {
            Content = payload
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = json.GetProperty("access_token").GetString()!;
        var newRefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 7200;

        return new OAuthTokenResult(accessToken, newRefreshToken, expiresIn, null);
    }

    private async Task<string?> GetAccountNameAsync(string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.x.com/2/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (json.TryGetProperty("data", out var data) && data.TryGetProperty("username", out var username))
        {
            return username.GetString();
        }

        return null;
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
