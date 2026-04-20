using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface IOAuthProvider
{
    PlatformType SupportedPlatform { get; }
    string GetAuthorizationUrl(OAuthPlatformConfig config, string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(OAuthPlatformConfig config, string code, CancellationToken ct = default);
    Task<OAuthTokenResult> RefreshTokenAsync(OAuthPlatformConfig config, string refreshToken, CancellationToken ct = default);
}
