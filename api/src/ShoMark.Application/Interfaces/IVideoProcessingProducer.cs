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
        CancellationToken ct = default);
}
