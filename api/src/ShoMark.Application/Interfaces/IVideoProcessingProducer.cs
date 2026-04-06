using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

/// <summary>
/// Publishes video-processing tasks to the message broker.
/// </summary>
public interface IVideoProcessingProducer
{
    Task SendProcessingRequestAsync(
        string videoBucket,
        string videoKey,
        string outputBucket,
        string outputPrefix,
        TargetAudience? targetAudience = null,
        string? description = null,
        CancellationToken ct = default);
}
