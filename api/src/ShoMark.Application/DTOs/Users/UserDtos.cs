namespace ShoMark.Application.DTOs.Users;

public record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt, DateTime UpdatedAt);

public record CreateUserRequest(string Name, string Email);

public record UpdateUserRequest(string Name, string Email);

public record UserWithPlatformsDto(
    Guid Id,
    string Name,
    string Email,
    DateTime CreatedAt,
    IReadOnlyList<PlatformSummaryDto> Platforms);

public record PlatformSummaryDto(Guid Id, string PlatformType, string? AccountName, DateTime? TokenExpiresAt);
