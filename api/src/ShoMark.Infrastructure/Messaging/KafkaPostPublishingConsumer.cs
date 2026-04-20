using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// Background service that consumes post-publishing messages from Kafka
/// and delegates actual publishing to IPostPublishingService.
/// </summary>
public class KafkaPostPublishingConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaPostPublishingConsumer> _logger;

    public KafkaPostPublishingConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<KafkaPostPublishingConsumer> logger)
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
            GroupId = $"{_options.ConsumerGroupId}-publishing",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.PostPublishingTopic);

        _logger.LogInformation(
            "Post publishing consumer started — listening on topic '{Topic}'",
            _options.PostPublishingTopic);

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
                _logger.LogError(ex, "Kafka consume error in post publishing consumer");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing post publishing message");
            }
        }

        consumer.Close();
        _logger.LogInformation("Post publishing consumer stopped");
    }

    private async Task HandleMessageAsync(string messageValue, CancellationToken ct)
    {
        if (!Guid.TryParse(messageValue, out var postId))
        {
            _logger.LogWarning("Invalid post ID in publishing message: {Message}", messageValue);
            return;
        }

        _logger.LogInformation("Processing publishing request for post {PostId}", postId);

        using var scope = _scopeFactory.CreateScope();
        var publishingService = scope.ServiceProvider.GetRequiredService<IPostPublishingService>();

        var result = await publishingService.PublishPostAsync(postId, ct);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Post {PostId} published successfully: {Url}",
                postId, result.Value?.ExternalUrl);
        }
        else
        {
            _logger.LogWarning("Post {PostId} publishing failed: {Error}", postId, result.Error);
        }
    }
}
