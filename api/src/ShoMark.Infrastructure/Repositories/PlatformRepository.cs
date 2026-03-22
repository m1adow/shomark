using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class PlatformRepository : Repository<Platform>, IPlatformRepository
{
    public PlatformRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Platform>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Platform>> GetExpiringTokensAsync(DateTime before, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(p => p.TokenExpiresAt != null && p.TokenExpiresAt < before)
            .ToListAsync(ct);
    }
}
