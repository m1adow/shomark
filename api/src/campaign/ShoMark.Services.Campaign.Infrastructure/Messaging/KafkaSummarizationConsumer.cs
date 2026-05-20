using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AppKafkaOptions = ShoMark.Application.Common.KafkaOptions;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// Background service that consumes video-summarization-completed events from Kafka.
/// Persists the AI-generated summary onto the Video entity and fires a
/// "transcription-complete" SSE event to any connected browser clients.
///
/// Worker message format:
/// {
///   "video_bucket": "videos",
///   "video_key": "path/to/video.mp4",
///   "summary": "This is a panel discussion about..."
/// }
/// </summary>
public class KafkaSummarizationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppKafkaOptions _options;
    private readonly ILogger<KafkaSummarizationConsumer> _logger;
    private readonly IVideoProcessingNotifier _notifier;

    public KafkaSummarizationConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<AppKafkaOptions> options,
        ILogger<KafkaSummarizationConsumer> logger,
        IVideoProcessingNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _notifier = notifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.SummarizationCompletedTopic);

        _logger.LogInformation(
            "Kafka summarization consumer started — listening on topic '{Topic}'",
            _options.SummarizationCompletedTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                await HandleMessageAsync(result.Message.Value, stoppingToken);
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing summarization message");
            }
        }

        consumer.Close();
        _logger.LogInformation("Kafka summarization consumer stopped");
    }

    private async Task HandleMessageAsync(string messageValue, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(messageValue);
        var root = doc.RootElement;

        var videoBucket = root.GetProperty("video_bucket").GetString()!;
        var videoKey = root.GetProperty("video_key").GetString()!;
        var summary = root.TryGetProperty("summary", out var sumEl) ? sumEl.GetString() : null;
        var storageKey = $"{videoBucket}/{videoKey}";

        _logger.LogInformation("Received summarization result for video: {VideoKey}", videoKey);

        using var scope = _scopeFactory.CreateScope();
        var videoRepo = scope.ServiceProvider.GetRequiredService<IVideoRepository>();

        var video = await videoRepo.GetByStorageKeyAsync(storageKey, ct);
        if (video is null)
        {
            _logger.LogWarning("Video not found for key {VideoKey} — skipping", videoKey);
            return;
        }

        video.Summary = summary;
        await videoRepo.UpdateAsync(video, ct);

        _logger.LogInformation("Summary saved for video {VideoId}", video.Id);

        var sseData = JsonSerializer.Serialize(new
        {
            videoId = video.Id,
            summary,
        });
        await _notifier.PublishAsync(video.Id, "transcription-complete", sseData);
    }
}
