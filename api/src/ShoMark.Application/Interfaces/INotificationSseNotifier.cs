using System.Threading.Channels;

namespace ShoMark.Application.Interfaces;

public interface INotificationSseNotifier
{
    ChannelReader<string> Subscribe(Guid userId);
    Task PublishAsync(Guid userId, string payload);
    void Unsubscribe(Guid userId, ChannelReader<string> reader);
}
