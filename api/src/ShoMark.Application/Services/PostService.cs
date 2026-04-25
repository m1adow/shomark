using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Posts;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Enums;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IAiFragmentRepository _fragmentRepository;
    private readonly IPlatformRepository _platformRepository;

    public PostService(
        IPostRepository postRepository,
        IAiFragmentRepository fragmentRepository,
        IPlatformRepository platformRepository)
    {
        _postRepository = postRepository;
        _fragmentRepository = fragmentRepository;
        _platformRepository = platformRepository;
    }

    public async Task<Result<PostDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var post = await _postRepository.GetByIdAsync(id, ct);
        if (post is null)
            return Result<PostDto>.Failure(Constants.Errors.Messages.PostNotFound, Constants.Errors.Codes.NotFound);

        return Result<PostDto>.Success(post.ToDto());
    }

    public async Task<Result<IReadOnlyList<PostDto>>> GetByFragmentIdAsync(Guid fragmentId, CancellationToken ct = default)
    {
        var posts = await _postRepository.GetByFragmentIdAsync(fragmentId, ct);
        return Result<IReadOnlyList<PostDto>>.Success(
            posts.Select(p => p.ToDto()).ToList());
    }

    public async Task<Result<IReadOnlyList<PostDto>>> GetByStatusAsync(PostStatus status, CancellationToken ct = default)
    {
        var posts = await _postRepository.GetByStatusAsync(status, ct);
        return Result<IReadOnlyList<PostDto>>.Success(
            posts.Select(p => p.ToDto()).ToList());
    }

    public async Task<Result<PostWithAnalyticsDto>> GetWithAnalyticsAsync(Guid id, CancellationToken ct = default)
    {
        var post = await _postRepository.GetWithAnalyticsAsync(id, ct);
        if (post is null)
            return Result<PostWithAnalyticsDto>.Failure(Constants.Errors.Messages.PostNotFound, Constants.Errors.Codes.NotFound);

        var analyticsDto = post.Analytics is not null
            ? new AnalyticsSummaryDto(
                post.Analytics.Views, post.Analytics.Likes,
                post.Analytics.Shares, post.Analytics.Comments,
                post.Analytics.LastSyncedAt)
            : null;

        var dto = new PostWithAnalyticsDto(
            post.Id, post.FragmentId, post.PlatformId, post.CampaignId, post.Title, post.Content,
            post.ExternalUrl, post.Status.ToString(), post.ScheduledAt, post.PublishedAt,
            post.CreatedAt, analyticsDto);

        return Result<PostWithAnalyticsDto>.Success(dto);
    }

    public async Task<Result<PostDto>> CreateAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var fragment = await _fragmentRepository.GetByIdAsync(request.FragmentId, ct);
        if (fragment is null)
            return Result<PostDto>.Failure(Constants.Errors.Messages.FragmentNotFound, Constants.Errors.Codes.NotFound);

        var platform = await _platformRepository.GetByIdAsync(request.PlatformId, ct);
        if (platform is null)
            return Result<PostDto>.Failure(Constants.Errors.Messages.PlatformNotFound, Constants.Errors.Codes.NotFound);

        var post = new Post
        {
            FragmentId = request.FragmentId,
            PlatformId = request.PlatformId,
            CampaignId = request.CampaignId,
            Title = request.Title,
            Content = request.Content,
            ScheduledAt = request.ScheduledAt,
            Status = request.ScheduledAt.HasValue ? PostStatus.Scheduled : PostStatus.Draft
        };

        var created = await _postRepository.AddAsync(post, ct);
        return Result<PostDto>.Success(created.ToDto());
    }

    public async Task<Result<PostDto>> UpdateAsync(Guid id, UpdatePostRequest request, CancellationToken ct = default)
    {
        var post = await _postRepository.GetByIdAsync(id, ct);
        if (post is null)
            return Result<PostDto>.Failure(Constants.Errors.Messages.PostNotFound, Constants.Errors.Codes.NotFound);

        if (request.Title is not null) post.Title = request.Title;
        if (request.Content is not null) post.Content = request.Content;
        if (request.ExternalUrl is not null) post.ExternalUrl = request.ExternalUrl;
        if (request.Status.HasValue) post.Status = request.Status.Value;
        if (request.ScheduledAt.HasValue) post.ScheduledAt = request.ScheduledAt.Value;
        if (request.PublishedAt.HasValue) post.PublishedAt = request.PublishedAt.Value;

        await _postRepository.UpdateAsync(post, ct);
        return Result<PostDto>.Success(post.ToDto());
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var post = await _postRepository.GetByIdAsync(id, ct);
        if (post is null)
            return Result<bool>.Failure(Constants.Errors.Messages.PostNotFound, Constants.Errors.Codes.NotFound);

        await _postRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyList<PostDto>>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default)
    {
        var posts = await _postRepository.GetByCampaignIdAsync(campaignId, ct);
        return Result<IReadOnlyList<PostDto>>.Success(
            posts.Select(p => p.ToDto()).ToList());
    }

    public async Task<Result<IReadOnlyList<PostDto>>> GetScheduledInRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var posts = await _postRepository.GetScheduledInRangeAsync(from, to, ct);
        return Result<IReadOnlyList<PostDto>>.Success(
            posts.Select(p => p.ToDto()).ToList());
    }
}
