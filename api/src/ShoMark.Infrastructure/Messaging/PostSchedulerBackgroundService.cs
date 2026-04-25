using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Enums;
using ShoMark.Infrastructure.Data;

namespace ShoMark.Infrastructure.Messaging;

/// <summary>
/// Background service that periodically scans for posts with Status=Scheduled
/// whose ScheduledAt time has passed, transitions them to Publishing status,
/// and produces a message to the post-publishing Kafka topic.
/// </summary>
public class PostSchedulerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPostPublishingProducer _producer;
    private readonly ILogger<PostSchedulerBackgroundService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    public PostSchedulerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IPostPublishingProducer producer,
        ILogger<PostSchedulerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _logger.LogInformation("Post scheduler started — polling every {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledPostsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in post scheduler loop");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Post scheduler stopped");
    }

    private async Task ProcessScheduledPostsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShoMarkDbContext>();

        var duePosts = await dbContext.Posts
            .Where(p => p.Status == PostStatus.Scheduled && p.ScheduledAt <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (duePosts.Count == 0) return;

        _logger.LogInformation("Found {Count} scheduled posts due for publishing", duePosts.Count);

        foreach (var post in duePosts)
        {
            // Transition to Publishing to prevent re-processing
            post.Status = PostStatus.Publishing;
            await dbContext.SaveChangesAsync(ct);

            try
            {
                await _producer.ProduceAsync(post.Id, ct);
                _logger.LogInformation("Produced publishing request for post {PostId}", post.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to produce publishing request for post {PostId}", post.Id);
                // Revert to Scheduled so it can be retried next cycle
                post.Status = PostStatus.Scheduled;
                await dbContext.SaveChangesAsync(ct);
            }
        }
    }
}
