using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

public class KafkaPostPublishingProducer : IPostPublishingProducer, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaPostPublishingProducer> _logger;

    public KafkaPostPublishingProducer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaPostPublishingProducer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task ProduceAsync(Guid postId, CancellationToken ct = default)
    {
        var result = await _producer.ProduceAsync(
            _options.PostPublishingTopic,
            new Message<Null, string> { Value = postId.ToString() },
            ct);

        _logger.LogInformation(
            "Sent post-publishing request to {Topic} [partition {Partition}, offset {Offset}]: PostId={PostId}",
            result.Topic, result.Partition.Value, result.Offset.Value, postId);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
