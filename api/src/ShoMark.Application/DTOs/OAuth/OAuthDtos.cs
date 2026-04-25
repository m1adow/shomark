namespace ShoMark.Application.DTOs.OAuth;

public record OAuthTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? AccountName);

public record OAuthConnectResponse(string AuthorizationUrl);
