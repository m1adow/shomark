using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface IOAuthProvider
{
    PlatformType SupportedPlatform { get; }
    OAuthAuthorizationResult GetAuthorizationUrl(OAuthPlatformConfig config, string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, string? codeVerifier, CancellationToken ct = default);
    Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default);
}
