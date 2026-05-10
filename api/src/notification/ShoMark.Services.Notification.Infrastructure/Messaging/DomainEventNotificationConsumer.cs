using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Contracts.Events;
using ShoMark.Domain.Enums;
using ShoMark.Application.Interfaces;
using ShoMark.Messaging;

namespace ShoMark.Infrastructure.Messaging;

public class DomainEventNotificationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<DomainEventNotificationConsumer> _logger;

    public DomainEventNotificationConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<DomainEventNotificationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"{_options.ConsumerGroupId}-notifications",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe([
            _options.FragmentApprovedTopic,
            _options.VideoProcessingSucceededTopic,
            _options.CampaignStatusChangedTopic,
            _options.PostPublishedTopic,
            _options.PostFailedTopic
        ]);

        _logger.LogInformation("Notification event consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                await HandleMessageAsync(result.Topic, result.Message.Value, stoppingToken);
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error while reading notification events");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification event");
            }
        }

        consumer.Close();
    }

    private async Task HandleMessageAsync(string topic, string payload, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        if (topic == _options.VideoProcessingSucceededTopic)
        {
            var @event = JsonSerializer.Deserialize<VideoProcessingCompletedEvent>(payload);
            if (@event is null) return;

            await notifications.CreateAsync(
                @event.UserId,
                NotificationType.VideoProcessingCompleted,
                "Video processing completed",
                $"{(@event.Title ?? "Video")} is ready with {@event.HighlightCount} highlights",
                @event.VideoId,
                ct);
            return;
        }

        if (topic == _options.FragmentApprovedTopic)
        {
            var @event = JsonSerializer.Deserialize<FragmentApprovedEvent>(payload);
            if (@event is null) return;

            await notifications.CreateAsync(
                @event.UserId,
                NotificationType.FragmentApproved,
                "Fragment approved",
                @event.Description,
                @event.FragmentId,
                ct);
            return;
        }

        if (topic == _options.CampaignStatusChangedTopic)
        {
            var @event = JsonSerializer.Deserialize<CampaignStatusChangedEvent>(payload);
            if (@event is null) return;

            await notifications.CreateAsync(
                @event.UserId,
                NotificationType.CampaignStatusChanged,
                "Campaign status changed",
                $"{(@event.Name ?? "Campaign")} moved from {@event.PreviousStatus} to {@event.NewStatus}",
                @event.CampaignId,
                ct);
            return;
        }

        if (topic == _options.PostPublishedTopic)
        {
            var @event = JsonSerializer.Deserialize<PostPublishedEvent>(payload);
            if (@event is null) return;

            await notifications.CreateAsync(
                @event.UserId,
                NotificationType.PostPublished,
                "Post published",
                @event.ExternalUrl,
                @event.PostId,
                ct);
            return;
        }

        if (topic == _options.PostFailedTopic)
        {
            var @event = JsonSerializer.Deserialize<PostFailedEvent>(payload);
            if (@event is null) return;

            await notifications.CreateAsync(
                @event.UserId,
                NotificationType.PostFailed,
                "Post failed",
                @event.ErrorMessage,
                @event.PostId,
                ct);
        }
    }
}
