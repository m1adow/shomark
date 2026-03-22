using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface ITagRepository : IRepository<Tag>
{
    Task<Tag?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default);
}
