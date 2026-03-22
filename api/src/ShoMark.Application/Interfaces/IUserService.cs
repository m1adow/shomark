using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Users;

namespace ShoMark.Application.Interfaces;

public interface IUserService
{
    Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UserDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<UserWithPlatformsDto>> GetWithPlatformsAsync(Guid id, CancellationToken ct = default);
    Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
