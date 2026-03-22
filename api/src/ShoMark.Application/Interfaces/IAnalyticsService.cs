using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Analytics;

namespace ShoMark.Application.Interfaces;

public interface IAnalyticsService
{
    Task<Result<AnalyticsDto>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);
    Task<Result<AnalyticsDto>> UpsertAsync(Guid postId, UpdateAnalyticsRequest request, CancellationToken ct = default);
}
