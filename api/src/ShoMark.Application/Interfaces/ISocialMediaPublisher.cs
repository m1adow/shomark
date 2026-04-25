using ShoMark.Application.DTOs.Publishing;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface ISocialMediaPublisher
{
    PlatformType SupportedPlatform { get; }
    Task<PublishResult> PublishPostAsync(PublishRequest request, CancellationToken ct = default);
}
