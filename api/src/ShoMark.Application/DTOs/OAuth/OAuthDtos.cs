namespace ShoMark.Application.DTOs.OAuth;

public record OAuthTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? AccountName);

public record OAuthConnectResponse(string AuthorizationUrl);

/// <summary>
/// Returned by GetAuthorizationUrl. CodeVerifier is non-null only for providers
/// that require PKCE (e.g. TikTok); it must be stored in the OAuth state cache
/// and passed to ExchangeCodeAsync on the callback.
/// </summary>
public record OAuthAuthorizationResult(string Url, string? CodeVerifier);
