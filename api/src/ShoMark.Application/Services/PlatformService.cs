using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Platforms;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class PlatformService : IPlatformService
{
    private readonly IPlatformRepository _platformRepository;
    private readonly ICurrentUserAccessor _currentUser;

    public PlatformService(IPlatformRepository platformRepository, ICurrentUserAccessor currentUser)
    {
        _platformRepository = platformRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PlatformDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<PlatformDto>.Failure("Platform not found", "NOT_FOUND");

        return Result<PlatformDto>.Success(MapToDto(platform));
    }

    public async Task<Result<IReadOnlyList<PlatformDto>>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var platforms = await _platformRepository.GetByUserIdAsync(userId, ct);
        return Result<IReadOnlyList<PlatformDto>>.Success(
            platforms.Select(MapToDto).ToList());
    }

    public async Task<Result<PlatformDto>> CreateAsync(CreatePlatformRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;

        var platform = new Platform
        {
            UserId = userId,
            PlatformType = request.PlatformType,
            AccountName = request.AccountName,
            AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken,
            TokenExpiresAt = request.TokenExpiresAt
        };

        var created = await _platformRepository.AddAsync(platform, ct);
        return Result<PlatformDto>.Success(MapToDto(created));
    }

    public async Task<Result<PlatformDto>> UpdateAsync(Guid id, UpdatePlatformRequest request, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<PlatformDto>.Failure("Platform not found", "NOT_FOUND");

        if (request.AccountName is not null) platform.AccountName = request.AccountName;
        if (request.AccessToken is not null) platform.AccessToken = request.AccessToken;
        if (request.RefreshToken is not null) platform.RefreshToken = request.RefreshToken;
        if (request.TokenExpiresAt.HasValue) platform.TokenExpiresAt = request.TokenExpiresAt.Value;

        await _platformRepository.UpdateAsync(platform, ct);
        return Result<PlatformDto>.Success(MapToDto(platform));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<bool>.Failure("Platform not found", "NOT_FOUND");

        await _platformRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    private static PlatformDto MapToDto(Platform p) => new(
        p.Id, p.UserId, p.PlatformType.ToString(), p.AccountName,
        p.TokenExpiresAt, p.CreatedAt, p.UpdatedAt);
}
