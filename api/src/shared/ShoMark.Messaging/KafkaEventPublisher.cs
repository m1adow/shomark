using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ShoMark.Messaging;

public sealed class KafkaEventPublisher : IKafkaEventPublisher, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;

        _producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All
        }).Build();
    }

    public async Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(@event);
        var result = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = payload }, ct);

        _logger.LogInformation(
            "Published {EventType} to {Topic} [partition {Partition}, offset {Offset}]",
            typeof(TEvent).Name,
            result.Topic,
            result.Partition.Value,
            result.Offset.Value);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
