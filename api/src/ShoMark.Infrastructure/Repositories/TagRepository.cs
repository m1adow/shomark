using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Repositories;

public class TagRepository : Repository<Tag>, ITagRepository
{
    public TagRepository(ShoMarkDbContext context) : base(context) { }

    public async Task<Tag?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Tag>> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default)
    {
        return await Context.FragmentTags
            .AsNoTracking()
            .Where(ft => ft.FragmentId == fragmentId)
            .Select(ft => ft.Tag)
            .ToListAsync(ct);
    }
}
