using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class VideoRepository : Repository<Video>, IVideoRepository
{
    public VideoRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<Video?> GetByMinioKeyAsync(string minioKey, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(v => v.MinioKey == minioKey, ct);
    }

    public async Task<IReadOnlyList<Video>> GetActiveVideosAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(v => v.DeletedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Video?> GetWithFragmentsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Include(v => v.Fragments)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }
}
