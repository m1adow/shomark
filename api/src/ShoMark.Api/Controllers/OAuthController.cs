using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.OAuth;
using ShoMark.Application.DTOs.Platforms;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthService _oAuthService;
    private readonly IPlatformService _platformService;
    private readonly ITokenEncryptionService _encryption;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMemoryCache _cache;

    public OAuthController(
        IOAuthService oAuthService,
        IPlatformService platformService,
        ITokenEncryptionService encryption,
        ICurrentUserAccessor currentUser,
        IMemoryCache cache)
    {
        _oAuthService = oAuthService;
        _platformService = platformService;
        _encryption = encryption;
        _currentUser = currentUser;
        _cache = cache;
    }

    /// <summary>
    /// Initiates the OAuth flow for a platform. Returns the authorization URL.
    /// </summary>
    [HttpGet("{platform}/connect")]
    public IActionResult Connect(PlatformType platform)
    {
        var state = GenerateState();
        var cacheKey = $"oauth_state:{_currentUser.UserId}:{platform}";
        _cache.Set(cacheKey, state, TimeSpan.FromMinutes(10));

        var authUrl = _oAuthService.GetAuthorizationUrl(platform, state);
        return Ok(new OAuthConnectResponse(authUrl));
    }

    /// <summary>
    /// Handles the OAuth callback. Exchanges the authorization code for tokens,
    /// encrypts them, and creates/updates the Platform entity.
    /// </summary>
    [HttpGet("{platform}/callback")]
    public async Task<IActionResult> Callback(
        PlatformType platform,
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken ct)
    {
        // Validate anti-CSRF state parameter
        var cacheKey = $"oauth_state:{_currentUser.UserId}:{platform}";
        if (!_cache.TryGetValue(cacheKey, out string? expectedState) || expectedState != state)
        {
            return BadRequest(new { error = Constants.Errors.Messages.InvalidOAuthState, errorCode = Constants.Errors.Codes.InvalidState });
        }
        _cache.Remove(cacheKey);

        // Exchange authorization code for tokens
        OAuthTokenResult tokenResult;
        try
        {
            tokenResult = await _oAuthService.ExchangeCodeAsync(platform, code, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Failed to exchange OAuth code: {ex.Message}", errorCode = Constants.Errors.Codes.OAuthExchangeFailed });
        }

        // Check if user already has this platform connected
        var existingPlatforms = await _platformService.GetByUserIdAsync(_currentUser.UserId, ct);
        var existing = existingPlatforms.Value?.FirstOrDefault(p =>
            p.PlatformType == platform.ToString());

        if (existing is not null)
        {
            // Update existing platform with new tokens
            var updateRequest = new UpdatePlatformRequest(
                tokenResult.AccountName,
                tokenResult.AccessToken,
                tokenResult.RefreshToken,
                DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn));

            var updateResult = await _platformService.UpdateAsync(existing.Id, updateRequest, ct);
            return updateResult.IsSuccess
                ? Ok(updateResult.Value)
                : BadRequest(new { updateResult.Error, updateResult.ErrorCode });
        }
        else
        {
            // Create new platform connection
            var createRequest = new CreatePlatformRequest(
                platform,
                tokenResult.AccountName,
                tokenResult.AccessToken,
                tokenResult.RefreshToken,
                DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn));

            var createResult = await _platformService.CreateAsync(createRequest, ct);
            return createResult.IsSuccess
                ? CreatedAtAction(nameof(PlatformsController.GetById),
                    "Platforms",
                    new { id = createResult.Value!.Id },
                    createResult.Value)
                : BadRequest(new { createResult.Error, createResult.ErrorCode });
        }
    }

    /// <summary>
    /// Disconnects a platform by removing the stored credentials.
    /// </summary>
    [HttpPost("{platform}/disconnect")]
    public async Task<IActionResult> Disconnect(PlatformType platform, CancellationToken ct)
    {
        var platforms = await _platformService.GetByUserIdAsync(_currentUser.UserId, ct);
        var existing = platforms.Value?.FirstOrDefault(p =>
            p.PlatformType == platform.ToString());

        if (existing is null)
            return NotFound(new { error = Constants.Errors.Messages.PlatformNotConnected, errorCode = Constants.Errors.Codes.NotFound });

        var result = await _platformService.DeleteAsync(existing.Id, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.Error, result.ErrorCode });
    }

    /// <summary>
    /// Manually refreshes the OAuth tokens for a connected platform.
    /// </summary>
    [HttpPost("{platform}/refresh")]
    public async Task<IActionResult> RefreshToken(PlatformType platform, CancellationToken ct)
    {
        var platforms = await _platformService.GetByUserIdAsync(_currentUser.UserId, ct);
        var existing = platforms.Value?.FirstOrDefault(p =>
            p.PlatformType == platform.ToString());

        if (existing is null)
            return NotFound(new { error = Constants.Errors.Messages.PlatformNotConnected, errorCode = Constants.Errors.Codes.NotFound });

        // Get decrypted tokens
        var tokensResult = await _platformService.GetDecryptedTokensAsync(existing.Id, ct);
        if (!tokensResult.IsSuccess)
            return BadRequest(new { tokensResult.Error, tokensResult.ErrorCode });

        var tokens = tokensResult.Value!;
        if (tokens.RefreshToken is null)
            return BadRequest(new { error = Constants.Errors.Messages.NoRefreshToken, errorCode = Constants.Errors.Codes.NoRefreshToken });

        OAuthTokenResult refreshed;
        try
        {
            refreshed = await _oAuthService.RefreshTokenAsync(platform, tokens.RefreshToken, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Failed to refresh token: {ex.Message}", errorCode = Constants.Errors.Codes.RefreshFailed });
        }

        var updateRequest = new UpdatePlatformRequest(
            refreshed.AccountName,
            refreshed.AccessToken,
            refreshed.RefreshToken,
            DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn));

        var updateResult = await _platformService.UpdateAsync(existing.Id, updateRequest, ct);
        return updateResult.IsSuccess
            ? Ok(updateResult.Value)
            : BadRequest(new { updateResult.Error, updateResult.ErrorCode });
    }

    private static string GenerateState()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
