using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Platforms;

namespace ShoMark.Application.Interfaces;

public interface IPlatformService
{
    Task<Result<PlatformDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PlatformDto>>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<PlatformDto>> CreateAsync(CreatePlatformRequest request, CancellationToken ct = default);
    Task<Result<PlatformDto>> UpdateAsync(Guid id, UpdatePlatformRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<DecryptedPlatformTokens>> GetDecryptedTokensAsync(Guid id, CancellationToken ct = default);
}
