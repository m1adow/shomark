using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface IFragmentProjectionRepository : IRepository<FragmentProjection>
{
    Task<FragmentProjection?> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default);
    Task UpsertAsync(FragmentProjection projection, CancellationToken ct = default);
}
