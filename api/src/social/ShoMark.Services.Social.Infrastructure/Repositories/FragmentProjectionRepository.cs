using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class FragmentProjectionRepository : Repository<FragmentProjection>, IFragmentProjectionRepository
{
    public FragmentProjectionRepository(SocialDbContext context) : base(context) { }

    public async Task<FragmentProjection?> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FragmentId == fragmentId, ct);
    }

    public async Task UpsertAsync(FragmentProjection projection, CancellationToken ct = default)
    {
        var existing = await DbSet.FirstOrDefaultAsync(f => f.FragmentId == projection.FragmentId, ct);
        if (existing is null)
        {
            await DbSet.AddAsync(projection, ct);
        }
        else
        {
            existing.VideoId = projection.VideoId;
            existing.UserId = projection.UserId;
            existing.Description = projection.Description;
            existing.StartTime = projection.StartTime;
            existing.EndTime = projection.EndTime;
            existing.StorageKey = projection.StorageKey;
            existing.ViralScore = projection.ViralScore;
            existing.Hashtags = projection.Hashtags;
            existing.ThumbnailKey = projection.ThumbnailKey;
            existing.ApprovedAt = projection.ApprovedAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await Context.SaveChangesAsync(ct);
    }
}
