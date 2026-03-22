using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface IAnalyticsRepository : IRepository<Analytics>
{
    Task<Analytics?> GetByPostIdAsync(Guid postId, CancellationToken ct = default);
}
