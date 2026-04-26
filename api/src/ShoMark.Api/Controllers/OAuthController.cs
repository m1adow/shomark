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
    private readonly IConfiguration _configuration;

    public OAuthController(
        IOAuthService oAuthService,
        IPlatformService platformService,
        ITokenEncryptionService encryption,
        ICurrentUserAccessor currentUser,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _oAuthService = oAuthService;
        _platformService = platformService;
        _encryption = encryption;
        _currentUser = currentUser;
        _cache = cache;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiates the OAuth flow for a platform. Returns the authorization URL.
    /// </summary>
    [HttpGet("{platform}/connect")]
    public IActionResult Connect(PlatformType platform)
    {
        var state = GenerateState();
        // Key by `state` so the anonymous callback can resolve the originating user.
        var authResult = _oAuthService.GetAuthorizationUrl(platform, state);
        _cache.Set(StateCacheKey(state), new OAuthStateEntry(_currentUser.UserId, platform, authResult.CodeVerifier), TimeSpan.FromMinutes(10));

        return Ok(new OAuthConnectResponse(authResult.Url));
    }

    /// <summary>
    /// Handles the OAuth callback. Exchanges the authorization code for tokens,
    /// encrypts them, and creates/updates the Platform entity.
    /// The browser arrives here via a top-level redirect from the platform with
    /// no JWT, so this endpoint is anonymous and trusts the cached state token.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{platform}/callback")]
    public async Task<IActionResult> Callback(
        PlatformType platform,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken ct)
    {
        // Provider-reported errors (user denied consent, invalid scope, etc.)
        if (!string.IsNullOrEmpty(error))
        {
            return RedirectToFrontend(platform, success: false, message: errorDescription ?? error);
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return RedirectToFrontend(platform, success: false, message: "Missing OAuth code or state");
        }

        // Validate anti-CSRF state and recover the originating user.
        if (!_cache.TryGetValue(StateCacheKey(state), out OAuthStateEntry? entry)
            || entry is null
            || entry.Platform != platform)
        {
            return RedirectToFrontend(platform, success: false, message: Constants.Errors.Messages.InvalidOAuthState);
        }
        _cache.Remove(StateCacheKey(state));

        // Exchange authorization code for tokens
        OAuthTokenResult tokenResult;
        try
        {
            tokenResult = await _oAuthService.ExchangeCodeAsync(platform, code, entry.CodeVerifier, ct);
        }
        catch (Exception ex)
        {
            return RedirectToFrontend(platform, success: false, message: $"Failed to exchange OAuth code: {ex.Message}");
        }

        // Check if user already has this platform connected
        var existingPlatforms = await _platformService.GetByUserIdAsync(entry.UserId, ct);
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
                ? RedirectToFrontend(platform, success: true)
                : RedirectToFrontend(platform, success: false, message: updateResult.Error);
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

            var createResult = await _platformService.CreateForUserAsync(entry.UserId, createRequest, ct);
            return createResult.IsSuccess
                ? RedirectToFrontend(platform, success: true)
                : RedirectToFrontend(platform, success: false, message: createResult.Error);
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

    private static string StateCacheKey(string state) => $"oauth_state:{state}";

    private IActionResult RedirectToFrontend(PlatformType platform, bool success, string? message = null)
    {
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        var status = success ? "success" : "error";
        var url = $"{baseUrl}/oauth/callback?status={status}&platform={platform}";
        if (!string.IsNullOrEmpty(message))
        {
            url += $"&message={Uri.EscapeDataString(message)}";
        }
        return Redirect(url);
    }

    private sealed record OAuthStateEntry(Guid UserId, PlatformType Platform, string? CodeVerifier);
}
