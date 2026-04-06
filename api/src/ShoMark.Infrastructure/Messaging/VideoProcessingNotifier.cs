using System.Collections.Concurrent;
using System.Threading.Channels;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// In-memory pub/sub for video processing completion events.
/// Registered as singleton — bridges the Kafka consumer to SSE endpoints.
/// </summary>
public class VideoProcessingNotifier : IVideoProcessingNotifier
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<Channel<string>>> _subscriptions = new();

    public ChannelReader<string> Subscribe(Guid videoId)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var bag = _subscriptions.GetOrAdd(videoId, _ => []);
        bag.Add(channel);

        return channel.Reader;
    }

    public void Unsubscribe(Guid videoId, ChannelReader<string> reader)
    {
        if (!_subscriptions.TryGetValue(videoId, out var bag)) return;

        // Rebuild bag without the matching channel
        var remaining = new ConcurrentBag<Channel<string>>();
        foreach (var ch in bag)
        {
            if (ch.Reader != reader)
                remaining.Add(ch);
            else
                ch.Writer.TryComplete();
        }

        _subscriptions.TryUpdate(videoId, remaining, bag);

        // Clean up empty entries
        if (remaining.IsEmpty)
            _subscriptions.TryRemove(videoId, out _);
    }

    public async Task PublishAsync(Guid videoId, string payload)
    {
        if (!_subscriptions.TryGetValue(videoId, out var bag)) return;

        foreach (var channel in bag)
        {
            await channel.Writer.WriteAsync(payload);
        }
    }
}
