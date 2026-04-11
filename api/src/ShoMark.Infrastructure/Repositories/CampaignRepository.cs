using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class CampaignRepository : Repository<Campaign>, ICampaignRepository
{
    public CampaignRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Campaign>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Campaign?> GetByUserAndFragmentAsync(Guid userId, Guid fragmentId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.FragmentId == fragmentId, ct);
    }

    public async Task<Campaign?> GetByUserAndNameAsync(Guid userId, string name, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == name, ct);
    }

    public async Task<IReadOnlyList<Campaign>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(c => c.VideoId == videoId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }
}
