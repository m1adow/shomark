using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Services;

public class OAuthService : IOAuthService
{
    private readonly Dictionary<PlatformType, IOAuthProvider> _providers;
    private readonly OAuthOptions _options;

    public OAuthService(IEnumerable<IOAuthProvider> providers, IOptions<OAuthOptions> options)
    {
        _options = options.Value;
        _providers = providers.ToDictionary(p => p.SupportedPlatform);
    }

    public string GetAuthorizationUrl(PlatformType platform, string state)
    {
        var provider = GetProvider(platform);
        var config = GetPlatformConfig(platform);
        return provider.GetAuthorizationUrl(config, state);
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(PlatformType platform, string code, CancellationToken ct = default)
    {
        var provider = GetProvider(platform);
        var config = GetPlatformConfig(platform);
        return await provider.ExchangeCodeAsync(config, code, ct);
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(PlatformType platform, string refreshToken, CancellationToken ct = default)
    {
        var provider = GetProvider(platform);
        var config = GetPlatformConfig(platform);
        return await provider.RefreshTokenAsync(config, refreshToken, ct);
    }

    private IOAuthProvider GetProvider(PlatformType platform)
    {
        if (!_providers.TryGetValue(platform, out var provider))
            throw new NotSupportedException($"OAuth is not supported for platform: {platform}");
        return provider;
    }

    private OAuthPlatformConfig GetPlatformConfig(PlatformType platform) => platform switch
    {
        PlatformType.Instagram => _options.Instagram,
        PlatformType.TikTok => _options.TikTok,
        PlatformType.YouTube => _options.YouTube,
        PlatformType.X => _options.X,
        _ => throw new NotSupportedException($"OAuth configuration missing for platform: {platform}")
    };
}
