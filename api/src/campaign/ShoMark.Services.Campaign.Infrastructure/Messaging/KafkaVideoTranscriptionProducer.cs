using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// Publishes video-transcription requests (Phase 1) to Kafka.
/// Message format: {"video_bucket": "...", "video_key": "..."}
/// </summary>
public sealed class KafkaVideoTranscriptionProducer : IVideoTranscriptionProducer, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaVideoTranscriptionProducer> _logger;

    public KafkaVideoTranscriptionProducer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaVideoTranscriptionProducer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task SendTranscriptionRequestAsync(
        string videoBucket,
        string videoKey,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            video_bucket = videoBucket,
            video_key = videoKey,
        });

        var result = await _producer.ProduceAsync(
            _options.VideoTranscriptionTopic,
            new Message<Null, string> { Value = payload },
            ct);

        _logger.LogInformation(
            "Sent video-transcription request to {Topic} [partition {Partition}, offset {Offset}]: {Key}",
            result.Topic, result.Partition.Value, result.Offset.Value, videoKey);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
