using System.Threading.Channels;
using ShoMark.Application.Common;

namespace ShoMark.Application.Interfaces;

/// <summary>
/// Pub/sub notification service for video processing events.
/// Singleton: Kafka consumers publish events, SSE endpoints subscribe.
/// </summary>
public interface IVideoProcessingNotifier
{
    /// <summary>Subscribe to SSE events for a specific video.</summary>
    ChannelReader<SseEvent> Subscribe(Guid videoId);

    /// <summary>Unsubscribe a previously created reader.</summary>
    void Unsubscribe(Guid videoId, ChannelReader<SseEvent> reader);

    /// <summary>Publish a typed event to all subscribers of the given video.</summary>
    Task PublishAsync(Guid videoId, string eventType, string data);
}
