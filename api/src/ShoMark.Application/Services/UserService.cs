using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Users;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return Result<UserDto>.Failure("User not found", "NOT_FOUND");

        return Result<UserDto>.Success(MapToDto(user));
    }

    public async Task<Result<IReadOnlyList<UserDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);
        return Result<IReadOnlyList<UserDto>>.Success(
            users.Select(MapToDto).ToList());
    }

    public async Task<Result<UserWithPlatformsDto>> GetWithPlatformsAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetWithPlatformsAsync(id, ct);
        if (user is null)
            return Result<UserWithPlatformsDto>.Failure("User not found", "NOT_FOUND");

        var dto = new UserWithPlatformsDto(
            user.Id, user.Name, user.Email, user.CreatedAt,
            user.Platforms.Select(p => new PlatformSummaryDto(
                p.Id, p.PlatformType.ToString(), p.AccountName, p.TokenExpiresAt)).ToList());

        return Result<UserWithPlatformsDto>.Success(dto);
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Result<UserDto>.Failure("A user with this email already exists", "DUPLICATE");

        var user = new User { Name = request.Name, Email = request.Email };
        var created = await _userRepository.AddAsync(user, ct);
        return Result<UserDto>.Success(MapToDto(created));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return Result<UserDto>.Failure("User not found", "NOT_FOUND");

        user.Name = request.Name;
        user.Email = request.Email;

        await _userRepository.UpdateAsync(user, ct);
        return Result<UserDto>.Success(MapToDto(user));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return Result<bool>.Failure("User not found", "NOT_FOUND");

        await _userRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    private static UserDto MapToDto(User u) => new(u.Id, u.Name, u.Email, u.CreatedAt, u.UpdatedAt);
}
