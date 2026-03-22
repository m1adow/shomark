using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class AiFragmentRepository : Repository<AiFragment>, IAiFragmentRepository
{
    public AiFragmentRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<IReadOnlyList<AiFragment>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(f => f.VideoId == videoId)
            .OrderBy(f => f.StartTime)
            .ToListAsync(ct);
    }

    public async Task<AiFragment?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Include(f => f.FragmentTags).ThenInclude(ft => ft.Tag)
            .Include(f => f.Posts)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }
}
