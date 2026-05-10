namespace ShoMark.Messaging;

public interface IKafkaEventPublisher
{
    Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken ct = default);
}
