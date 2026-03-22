using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface IAiFragmentRepository : IRepository<AiFragment>
{
    Task<IReadOnlyList<AiFragment>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default);
    Task<AiFragment?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
