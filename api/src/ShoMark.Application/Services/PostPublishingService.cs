using Microsoft.Extensions.Logging;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Posts;
using ShoMark.Application.DTOs.Publishing;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Enums;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class PostPublishingService : IPostPublishingService
{
    private readonly IPostRepository _postRepository;
    private readonly IPlatformService _platformService;
    private readonly IOAuthService _oAuthService;
    private readonly IStorageService _storageService;
    private readonly IAiFragmentRepository _fragmentRepository;
    private readonly Dictionary<PlatformType, ISocialMediaPublisher> _publishers;
    private readonly ILogger<PostPublishingService> _logger;

    public PostPublishingService(
        IPostRepository postRepository,
        IPlatformService platformService,
        IOAuthService oAuthService,
        IStorageService storageService,
        IAiFragmentRepository fragmentRepository,
        IEnumerable<ISocialMediaPublisher> publishers,
        ILogger<PostPublishingService> logger)
    {
        _postRepository = postRepository;
        _platformService = platformService;
        _oAuthService = oAuthService;
        _storageService = storageService;
        _fragmentRepository = fragmentRepository;
        _publishers = publishers.ToDictionary(p => p.SupportedPlatform);
        _logger = logger;
    }

    public async Task<Result<PostDto>> PublishPostAsync(Guid postId, CancellationToken ct = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, ct);
        if (post is null)
            return Result<PostDto>.Failure(Constants.Errors.Messages.PostNotFound, Constants.Errors.Codes.NotFound);

        if (post.Status == PostStatus.Published)
            return Result<PostDto>.Failure(Constants.Errors.Messages.PostAlreadyPublished, Constants.Errors.Codes.AlreadyPublished);

        // Get decrypted platform tokens
        var tokensResult = await _platformService.GetDecryptedTokensAsync(post.PlatformId, ct);
        if (!tokensResult.IsSuccess)
            return Result<PostDto>.Failure(Constants.Errors.Messages.PlatformNotFound, Constants.Errors.Codes.PlatformNotFound);

        var tokens = tokensResult.Value!;

        // Auto-refresh if token is expired or near expiry (< 5 min)
        var accessToken = tokens.AccessToken;
        if (accessToken is null)
            return Result<PostDto>.Failure(Constants.Errors.Messages.NoAccessToken, Constants.Errors.Codes.NoAccessToken);

        if (tokens.TokenExpiresAt.HasValue &&
            tokens.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
        {
            if (tokens.RefreshToken is null)
            {
                await MarkPostFailedAsync(post, Constants.Errors.Messages.TokenExpiredNoRefresh, ct);
                return Result<PostDto>.Failure(Constants.Errors.Messages.TokenExpiredNoRefresh, Constants.Errors.Codes.TokenExpired);
            }

            try
            {
                var refreshed = await _oAuthService.RefreshTokenAsync(tokens.PlatformType, tokens.RefreshToken, ct);
                accessToken = refreshed.AccessToken;

                // Update platform with new encrypted tokens
                var updateRequest = new DTOs.Platforms.UpdatePlatformRequest(
                    refreshed.AccountName,
                    refreshed.AccessToken,
                    refreshed.RefreshToken,
                    DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn));
                await _platformService.UpdateAsync(tokens.PlatformId, updateRequest, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh token for platform {PlatformId}", tokens.PlatformId);
                await MarkPostFailedAsync(post, $"Token refresh failed: {ex.Message}", ct);
                return Result<PostDto>.Failure($"Token refresh failed: {ex.Message}", Constants.Errors.Codes.RefreshFailed);
            }
        }

        // Get media URL from the fragment
        string? mediaUrl = null;
        string? mediaContentType = Constants.ContentTypes.VideoMp4;
        var fragment = await _fragmentRepository.GetByIdAsync(post.FragmentId, ct);
        if (fragment?.MinioKey is not null)
        {
            var parts = fragment.MinioKey.Split('/', 2);
            if (parts.Length == 2)
            {
                mediaUrl = await _storageService.GetPresignedUrlAsync(parts[0], parts[1], Constants.Storage.DefaultPresignedUrlExpiry, ct);
            }
        }

        // Get the publisher for this platform
        if (!_publishers.TryGetValue(tokens.PlatformType, out var publisher))
        {
            await MarkPostFailedAsync(post, $"No publisher available for {tokens.PlatformType}", ct);
            return Result<PostDto>.Failure($"Publishing not supported for {tokens.PlatformType}", Constants.Errors.Codes.UnsupportedPlatform);
        }

        // Publish
        var publishRequest = new PublishRequest(
            accessToken,
            post.Title,
            post.Content,
            mediaUrl,
            mediaContentType);

        var result = await publisher.PublishPostAsync(publishRequest, ct);

        if (result.Success)
        {
            post.Status = PostStatus.Published;
            post.PublishedAt = DateTime.UtcNow;
            post.ExternalUrl = result.ExternalUrl;
            await _postRepository.UpdateAsync(post, ct);

            _logger.LogInformation("Post {PostId} published to {Platform}: {Url}",
                postId, tokens.PlatformType, result.ExternalUrl);
        }
        else
        {
            await MarkPostFailedAsync(post, result.ErrorMessage ?? Constants.Errors.Messages.UnknownPublishingError, ct);
            return Result<PostDto>.Failure(result.ErrorMessage ?? Constants.Errors.Messages.PublishingFailed, Constants.Errors.Codes.PublishFailed);
        }

        return Result<PostDto>.Success(post.ToDto());
    }

    private async Task MarkPostFailedAsync(Domain.Entities.Post post, string error, CancellationToken ct)
    {
        post.Status = PostStatus.Failed;
        await _postRepository.UpdateAsync(post, ct);
        _logger.LogWarning("Post {PostId} marked as failed: {Error}", post.Id, error);
    }
}
