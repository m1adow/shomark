using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class AnalyticsRepository : Repository<Analytics>, IAnalyticsRepository
{
    public AnalyticsRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<Analytics?> GetByPostIdAsync(Guid postId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PostId == postId, ct);
    }
}
