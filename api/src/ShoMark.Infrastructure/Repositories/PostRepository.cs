using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Enums;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Post>> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(p => p.FragmentId == fragmentId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Post>> GetByStatusAsync(PostStatus status, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Post?> GetWithAnalyticsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Include(p => p.Analytics)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }
}
