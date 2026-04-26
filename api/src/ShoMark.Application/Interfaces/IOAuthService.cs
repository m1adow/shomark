using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface IOAuthService
{
    OAuthAuthorizationResult GetAuthorizationUrl(PlatformType platform, string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(PlatformType platform, string code, string? codeVerifier, CancellationToken ct = default);
    Task<OAuthTokenResult> RefreshTokenAsync(PlatformType platform, string refreshToken, CancellationToken ct = default);
}
