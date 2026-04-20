using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Posts;

namespace ShoMark.Application.Interfaces;

public interface IPostPublishingService
{
    Task<Result<PostDto>> PublishPostAsync(Guid postId, CancellationToken ct = default);
}
