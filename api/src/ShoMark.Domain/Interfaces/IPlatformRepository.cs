using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface IPlatformRepository : IRepository<Platform>
{
    Task<IReadOnlyList<Platform>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Platform>> GetExpiringTokensAsync(DateTime before, CancellationToken ct = default);
}
