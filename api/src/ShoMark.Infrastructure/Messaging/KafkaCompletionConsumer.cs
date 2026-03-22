using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// Background service that consumes video-processing-completed events from Kafka
/// and persists the resulting AiFragment + Tag records into the database.
///
/// Worker completion message format:
/// {
///   "video_bucket": "videos",
///   "video_key": "path/to/video.mp4",
///   "output_bucket": "highlights",
///   "highlights": [
///     {
///       "key": "event-123/highlight_1.mp4",
///       "preview_key": "event-123/highlight_1_preview.jpg",
///       "meta_key": "event-123/highlight_1.mp4.json",
///       "title": "...",
///       "start": 10.5,
///       "end": 70.5
///     }
///   ]
/// }
/// </summary>
public class KafkaCompletionConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaCompletionConsumer> _logger;

    public KafkaCompletionConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<KafkaCompletionConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the rest of the app start first
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.CompletionTopic);

        _logger.LogInformation(
            "Kafka completion consumer started — listening on topic '{Topic}'",
            _options.CompletionTopic);

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
                _logger.LogError(ex, "Error processing completion message");
                // Continue consuming — don't crash the background service
            }
        }

        consumer.Close();
        _logger.LogInformation("Kafka completion consumer stopped");
    }

    private async Task HandleMessageAsync(string messageValue, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(messageValue);
        var root = doc.RootElement;

        var videoKey = root.GetProperty("video_key").GetString()!;
        var outputBucket = root.GetProperty("output_bucket").GetString()!;

        _logger.LogInformation("Processing completion for video: {VideoKey}", videoKey);

        if (!root.TryGetProperty("highlights", out var highlightsElement))
        {
            _logger.LogWarning("Completion message has no highlights for {VideoKey}", videoKey);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var videoRepo = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
        var fragmentRepo = scope.ServiceProvider.GetRequiredService<IAiFragmentRepository>();
        var tagRepo = scope.ServiceProvider.GetRequiredService<ITagRepository>();

        // Find the source video by MinIO key
        var video = await videoRepo.GetByMinioKeyAsync(videoKey, ct);
        if (video is null)
        {
            _logger.LogWarning("Video not found for key {VideoKey} — skipping", videoKey);
            return;
        }

        foreach (var highlight in highlightsElement.EnumerateArray())
        {
            var clipKey = highlight.GetProperty("key").GetString()!;
            var title = highlight.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
            var start = highlight.GetProperty("start").GetDouble();
            var end = highlight.GetProperty("end").GetDouble();

            var fragment = new AiFragment
            {
                VideoId = video.Id,
                Description = title,
                StartTime = start,
                EndTime = end,
                MinioKey = $"{outputBucket}/{clipKey}"
            };

            // Auto-tag with a "ai-generated" tag
            var slug = "ai-generated";
            var tag = await tagRepo.GetBySlugAsync(slug, ct);
            if (tag is null)
            {
                tag = new Tag { Name = "AI Generated", Slug = slug };
                tag = await tagRepo.AddAsync(tag, ct);
            }

            fragment.FragmentTags.Add(new FragmentTag
            {
                TagId = tag.Id
            });

            await fragmentRepo.AddAsync(fragment, ct);

            _logger.LogInformation(
                "Created fragment {FragmentId} for video {VideoId}: {Start}s-{End}s '{Title}'",
                fragment.Id, video.Id, start, end, title);
        }

        _logger.LogInformation(
            "Completed processing {Count} highlights for video {VideoKey}",
            highlightsElement.GetArrayLength(), videoKey);
    }
}
