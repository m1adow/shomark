namespace ShoMark.Application.Interfaces;

/// <summary>
/// Sends a transcription-only request to the worker via Kafka.
/// Phase 1 of the two-phase pipeline: download → transcribe → summarize.
/// </summary>
public interface IVideoTranscriptionProducer
{
    Task SendTranscriptionRequestAsync(
        string videoBucket,
        string videoKey,
        CancellationToken ct = default);
}
