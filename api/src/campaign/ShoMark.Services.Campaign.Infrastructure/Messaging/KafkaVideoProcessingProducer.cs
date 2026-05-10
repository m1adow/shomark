using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Videos;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// Kafka producer that sends video-processing requests to the worker.
/// Message format:
/// {"video_bucket": "...", "video_key": "...", "output_bucket": "...", "output_prefix": "...",
///  "target_audience": "Applicants|Masters|Professionals", "description": "...",
///  "additional_instructions": "...",
///  "exclude_ranges": [{"start": 10.5, "end": 70.5}, ...]}
/// </summary>
public class KafkaVideoProcessingProducer : IVideoProcessingProducer, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaVideoProcessingProducer> _logger;

    public KafkaVideoProcessingProducer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaVideoProcessingProducer> logger)
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

    public async Task SendProcessingRequestAsync(
        string videoBucket,
        string videoKey,
        string outputBucket,
        string outputPrefix,
        TargetAudience? targetAudience = null,
        string? description = null,
        string? additionalInstructions = null,
        IReadOnlyList<ExcludeRange>? excludeRanges = null,
        CancellationToken ct = default)
    {
        var message = new
        {
            video_bucket = videoBucket,
            video_key = videoKey,
            output_bucket = outputBucket,
            output_prefix = outputPrefix,
            target_audience = targetAudience?.ToString(),
            description,
            additional_instructions = additionalInstructions,
            exclude_ranges = excludeRanges?.Select(r => new { start = r.Start, end = r.End }).ToArray(),
        };

        var payload = JsonSerializer.Serialize(message);

        var result = await _producer.ProduceAsync(
            _options.VideoProcessingTopic,
            new Message<Null, string> { Value = payload },
            ct);

        _logger.LogInformation(
            "Sent video-processing request to {Topic} [partition {Partition}, offset {Offset}]: {Key}",
            result.Topic, result.Partition.Value, result.Offset.Value, videoKey);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
