using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Posts;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface IPostService
{
    Task<Result<PostDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PostDto>>> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PostDto>>> GetByStatusAsync(PostStatus status, CancellationToken ct = default);
    Task<Result<PostWithAnalyticsDto>> GetWithAnalyticsAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PostDto>>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PostDto>>> GetScheduledInRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<Result<PostDto>> CreateAsync(CreatePostRequest request, CancellationToken ct = default);
    Task<Result<PostDto>> UpdateAsync(Guid id, UpdatePostRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
