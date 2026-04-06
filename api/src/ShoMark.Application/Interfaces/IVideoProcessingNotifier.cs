using System.Threading.Channels;

namespace ShoMark.Application.Interfaces;

/// <summary>
/// Pub/sub notification service for video processing completion.
/// Singleton: the Kafka consumer publishes events, SSE endpoints subscribe.
/// </summary>
public interface IVideoProcessingNotifier
{
    /// <summary>
    /// Subscribe to completion events for a specific video.
    /// Returns a ChannelReader that yields event payloads (JSON strings).
    /// </summary>
    ChannelReader<string> Subscribe(Guid videoId);

    /// <summary>
    /// Unsubscribe a previously created reader for a video.
    /// </summary>
    void Unsubscribe(Guid videoId, ChannelReader<string> reader);

    /// <summary>
    /// Publish a completion event to all subscribers of the given video.
    /// </summary>
    Task PublishAsync(Guid videoId, string payload);
}
