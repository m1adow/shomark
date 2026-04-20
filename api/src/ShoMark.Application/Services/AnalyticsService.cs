using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Analytics;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly IPostRepository _postRepository;

    public AnalyticsService(IAnalyticsRepository analyticsRepository, IPostRepository postRepository)
    {
        _analyticsRepository = analyticsRepository;
        _postRepository = postRepository;
    }

    public async Task<Result<AnalyticsDto>> GetByPostIdAsync(Guid postId, CancellationToken ct = default)
    {
        var analytics = await _analyticsRepository.GetByPostIdAsync(postId, ct);
        if (analytics is null)
            return Result<AnalyticsDto>.Failure(Constants.Errors.Messages.AnalyticsNotFound, Constants.Errors.Codes.NotFound);

        return Result<AnalyticsDto>.Success(analytics.ToDto());
    }

    public async Task<Result<AnalyticsDto>> UpsertAsync(Guid postId, UpdateAnalyticsRequest request, CancellationToken ct = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, ct);
        if (post is null)
            return Result<AnalyticsDto>.Failure(Constants.Errors.Messages.PostNotFound, Constants.Errors.Codes.NotFound);

        var analytics = await _analyticsRepository.GetByPostIdAsync(postId, ct);

        if (analytics is null)
        {
            analytics = new Analytics
            {
                PostId = postId,
                Views = request.Views,
                Likes = request.Likes,
                Shares = request.Shares,
                Comments = request.Comments,
                LastSyncedAt = DateTime.UtcNow
            };
            var created = await _analyticsRepository.AddAsync(analytics, ct);
            return Result<AnalyticsDto>.Success(created.ToDto());
        }

        analytics.Views = request.Views;
        analytics.Likes = request.Likes;
        analytics.Shares = request.Shares;
        analytics.Comments = request.Comments;
        analytics.LastSyncedAt = DateTime.UtcNow;

        await _analyticsRepository.UpdateAsync(analytics, ct);
        return Result<AnalyticsDto>.Success(analytics.ToDto());
    }
}
