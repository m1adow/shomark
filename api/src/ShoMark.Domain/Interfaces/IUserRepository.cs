using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetWithPlatformsAsync(Guid id, CancellationToken ct = default);
}
