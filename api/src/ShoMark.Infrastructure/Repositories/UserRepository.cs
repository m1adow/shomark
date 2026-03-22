using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetWithPlatformsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Include(u => u.Platforms)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }
}
