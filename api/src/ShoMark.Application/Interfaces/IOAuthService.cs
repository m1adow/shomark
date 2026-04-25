using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface IOAuthService
{
    string GetAuthorizationUrl(PlatformType platform, string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(PlatformType platform, string code, CancellationToken ct = default);
    Task<OAuthTokenResult> RefreshTokenAsync(PlatformType platform, string refreshToken, CancellationToken ct = default);
}
