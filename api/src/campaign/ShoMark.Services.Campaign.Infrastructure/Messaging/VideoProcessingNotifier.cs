using System.Collections.Concurrent;
using System.Threading.Channels;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// In-memory pub/sub for video SSE events.
/// Registered as singleton — bridges Kafka consumers to SSE endpoints.
/// </summary>
public class VideoProcessingNotifier : IVideoProcessingNotifier
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<Channel<SseEvent>>> _subscriptions = new();

    public ChannelReader<SseEvent> Subscribe(Guid videoId)
    {
        var channel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var bag = _subscriptions.GetOrAdd(videoId, _ => []);
        bag.Add(channel);

        return channel.Reader;
    }

    public void Unsubscribe(Guid videoId, ChannelReader<SseEvent> reader)
    {
        if (!_subscriptions.TryGetValue(videoId, out var bag)) return;

        var remaining = new ConcurrentBag<Channel<SseEvent>>();
        foreach (var ch in bag)
        {
            if (ch.Reader != reader)
                remaining.Add(ch);
            else
                ch.Writer.TryComplete();
        }

        _subscriptions.TryUpdate(videoId, remaining, bag);

        if (remaining.IsEmpty)
            _subscriptions.TryRemove(videoId, out _);
    }

    public async Task PublishAsync(Guid videoId, string eventType, string data)
    {
        if (!_subscriptions.TryGetValue(videoId, out var bag)) return;

        var @event = new SseEvent(eventType, data);
        foreach (var channel in bag)
        {
            await channel.Writer.WriteAsync(@event);
        }
    }
}
