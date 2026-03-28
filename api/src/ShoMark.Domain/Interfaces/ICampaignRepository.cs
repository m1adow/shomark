using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface ICampaignRepository : IRepository<Campaign>
{
    Task<IReadOnlyList<Campaign>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Campaign?> GetByUserAndFragmentAsync(Guid userId, Guid fragmentId, CancellationToken ct = default);
    Task<IReadOnlyList<Campaign>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default);
}
