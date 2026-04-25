using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Platforms;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class PlatformService : IPlatformService
{
    private readonly IPlatformRepository _platformRepository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ITokenEncryptionService _encryption;

    public PlatformService(
        IPlatformRepository platformRepository,
        ICurrentUserAccessor currentUser,
        ITokenEncryptionService encryption)
    {
        _platformRepository = platformRepository;
        _currentUser = currentUser;
        _encryption = encryption;
    }

    public async Task<Result<PlatformDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<PlatformDto>.Failure(Constants.Errors.Messages.PlatformNotFound, Constants.Errors.Codes.NotFound);

        return Result<PlatformDto>.Success(platform.ToDto());
    }

    public async Task<Result<IReadOnlyList<PlatformDto>>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var platforms = await _platformRepository.GetByUserIdAsync(userId, ct);
        return Result<IReadOnlyList<PlatformDto>>.Success(
            platforms.Select(p => p.ToDto()).ToList());
    }

    public async Task<Result<PlatformDto>> CreateAsync(CreatePlatformRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;

        var platform = new Platform
        {
            UserId = userId,
            PlatformType = request.PlatformType,
            AccountName = request.AccountName,
            AccessToken = request.AccessToken is not null ? _encryption.Encrypt(request.AccessToken) : null,
            RefreshToken = request.RefreshToken is not null ? _encryption.Encrypt(request.RefreshToken) : null,
            TokenExpiresAt = request.TokenExpiresAt
        };

        var created = await _platformRepository.AddAsync(platform, ct);
        return Result<PlatformDto>.Success(created.ToDto());
    }

    public async Task<Result<PlatformDto>> UpdateAsync(Guid id, UpdatePlatformRequest request, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<PlatformDto>.Failure(Constants.Errors.Messages.PlatformNotFound, Constants.Errors.Codes.NotFound);

        if (request.AccountName is not null) platform.AccountName = request.AccountName;
        if (request.AccessToken is not null) platform.AccessToken = _encryption.Encrypt(request.AccessToken);
        if (request.RefreshToken is not null) platform.RefreshToken = _encryption.Encrypt(request.RefreshToken);
        if (request.TokenExpiresAt.HasValue) platform.TokenExpiresAt = request.TokenExpiresAt.Value;

        await _platformRepository.UpdateAsync(platform, ct);
        return Result<PlatformDto>.Success(platform.ToDto());
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<bool>.Failure(Constants.Errors.Messages.PlatformNotFound, Constants.Errors.Codes.NotFound);

        await _platformRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<DecryptedPlatformTokens>> GetDecryptedTokensAsync(Guid id, CancellationToken ct = default)
    {
        var platform = await _platformRepository.GetByIdAsync(id, ct);
        if (platform is null)
            return Result<DecryptedPlatformTokens>.Failure(Constants.Errors.Messages.PlatformNotFound, Constants.Errors.Codes.NotFound);

        var accessToken = platform.AccessToken is not null ? _encryption.Decrypt(platform.AccessToken) : null;
        var refreshToken = platform.RefreshToken is not null ? _encryption.Decrypt(platform.RefreshToken) : null;

        return Result<DecryptedPlatformTokens>.Success(new DecryptedPlatformTokens(
            platform.Id, platform.PlatformType, accessToken, refreshToken, platform.TokenExpiresAt));
    }

}
