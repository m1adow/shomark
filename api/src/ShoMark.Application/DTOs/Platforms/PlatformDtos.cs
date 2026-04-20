using ShoMark.Domain.Enums;

namespace ShoMark.Application.DTOs.Platforms;

public record PlatformDto(
    Guid Id,
    Guid UserId,
    string PlatformType,
    string? AccountName,
    DateTime? TokenExpiresAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreatePlatformRequest(
    PlatformType PlatformType,
    string? AccountName,
    string? AccessToken,
    string? RefreshToken,
    DateTime? TokenExpiresAt);

public record UpdatePlatformRequest(
    string? AccountName,
    string? AccessToken,
    string? RefreshToken,
    DateTime? TokenExpiresAt);

public record DecryptedPlatformTokens(
    Guid PlatformId,
    PlatformType PlatformType,
    string? AccessToken,
    string? RefreshToken,
    DateTime? TokenExpiresAt);
