using System.Collections.Concurrent;
using System.Threading.Channels;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// In-memory pub/sub for notification events keyed by user ID.
/// Registered as singleton — bridges services to SSE endpoints.
/// </summary>
public class NotificationSseNotifier : INotificationSseNotifier
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<Channel<string>>> _subscriptions = new();

    public ChannelReader<string> Subscribe(Guid userId)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var bag = _subscriptions.GetOrAdd(userId, _ => []);
        bag.Add(channel);

        return channel.Reader;
    }

    public void Unsubscribe(Guid userId, ChannelReader<string> reader)
    {
        if (!_subscriptions.TryGetValue(userId, out var bag)) return;

        var remaining = new ConcurrentBag<Channel<string>>();
        foreach (var ch in bag)
        {
            if (ch.Reader != reader)
                remaining.Add(ch);
            else
                ch.Writer.TryComplete();
        }

        _subscriptions.TryUpdate(userId, remaining, bag);

        if (remaining.IsEmpty)
            _subscriptions.TryRemove(userId, out _);
    }

    public async Task PublishAsync(Guid userId, string payload)
    {
        if (!_subscriptions.TryGetValue(userId, out var bag)) return;

        foreach (var channel in bag)
        {
            await channel.Writer.WriteAsync(payload);
        }
    }
}
