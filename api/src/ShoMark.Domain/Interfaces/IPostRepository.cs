using ShoMark.Domain.Entities;
using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Interfaces;

public interface IPostRepository : IRepository<Post>
{
    Task<IReadOnlyList<Post>> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetByStatusAsync(PostStatus status, CancellationToken ct = default);
    Task<Post?> GetWithAnalyticsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetScheduledInRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
