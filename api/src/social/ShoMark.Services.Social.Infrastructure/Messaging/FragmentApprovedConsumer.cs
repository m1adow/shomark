using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Contracts.Events;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;
using ShoMark.Messaging;

namespace ShoMark.Infrastructure.Messaging;

public class FragmentApprovedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<FragmentApprovedConsumer> _logger;

    public FragmentApprovedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<FragmentApprovedConsumer> logger)
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
            GroupId = $"{_options.ConsumerGroupId}-social-fragments",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.FragmentApprovedTopic);

        _logger.LogInformation("Fragment projection consumer started on topic {Topic}", _options.FragmentApprovedTopic);

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
                _logger.LogError(ex, "Kafka consume error while reading fragment approvals");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing fragment-approved event");
            }
        }

        consumer.Close();
    }

    private async Task HandleMessageAsync(string payload, CancellationToken ct)
    {
        var @event = JsonSerializer.Deserialize<FragmentApprovedEvent>(payload);
        if (@event is null)
        {
            _logger.LogWarning("Skipping malformed fragment-approved event: {Payload}", payload);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var projectionRepository = scope.ServiceProvider.GetRequiredService<IFragmentProjectionRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SocialDbContext>();

        var projection = new FragmentProjection
        {
            FragmentId = @event.FragmentId,
            VideoId = @event.VideoId,
            UserId = @event.UserId,
            Description = @event.Description,
            StartTime = @event.StartTime,
            EndTime = @event.EndTime,
            StorageKey = @event.StorageKey,
            ViralScore = @event.ViralScore,
            Hashtags = @event.Hashtags,
            ThumbnailKey = @event.ThumbnailKey,
            ApprovedAt = @event.ApprovedAt
        };

        await projectionRepository.UpsertAsync(projection, ct);

        await dbContext.Posts
            .Where(p => p.FragmentId == @event.FragmentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.FragmentDescription, @event.Description)
                .SetProperty(p => p.FragmentStartTime, @event.StartTime)
                .SetProperty(p => p.FragmentEndTime, @event.EndTime)
                .SetProperty(p => p.FragmentStorageKey, @event.StorageKey)
                .SetProperty(p => p.FragmentThumbnailKey, @event.ThumbnailKey), ct);

        _logger.LogInformation("Projected approved fragment {FragmentId}", @event.FragmentId);
    }
}
