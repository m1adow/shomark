using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShoMark.Analytics.Infrastructure.Entities;
using ShoMark.Analytics.Infrastructure.Persistence;
using ShoMark.Messaging;

namespace ShoMark.Analytics.Infrastructure.BackgroundServices;

/// <summary>
/// Runs every 60 minutes and snapshots metrics for all posts published in the last 30 days.
/// When a platform token is expired and refresh fails, emits a platform-token-expired Kafka event.
/// </summary>
public class MetricsSyncService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaEventPublisher _kafka;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<MetricsSyncService> _logger;

    public MetricsSyncService(
        IServiceScopeFactory scopeFactory,
        IKafkaEventPublisher kafka,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<MetricsSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _kafka = kafka;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricsSyncService started. Sync interval: {Interval}", Interval);

        // Run once on startup, then on the timer
        await RunSyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunSyncAsync(stoppingToken);
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting metrics sync at {Time}", DateTime.UtcNow);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var socialDb = scope.ServiceProvider.GetRequiredService<SocialReadContext>();
            var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

            var cutoff = DateTime.UtcNow - LookbackWindow;

            var posts = await socialDb.Posts
                .Include(p => p.Platform)
                .Where(p =>
                    p.Status == "Published" &&
                    p.ExternalPostId != null &&
                    p.PublishedAt >= cutoff)
                .ToListAsync(ct);

            _logger.LogInformation("Found {Count} posts to sync", posts.Count);

            foreach (var post in posts)
            {
                await SyncPostAsync(post, analyticsDb, ct);
            }

            await analyticsDb.SaveChangesAsync(ct);
            _logger.LogInformation("Metrics sync completed. Synced {Count} posts", posts.Count);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics sync");
        }
    }

    private async Task SyncPostAsync(
        PostReadModel post,
        AnalyticsDbContext analyticsDb,
        CancellationToken ct)
    {
        try
        {
            // Check token expiry
            if (post.Platform.TokenExpiresAt.HasValue &&
                post.Platform.TokenExpiresAt.Value <= DateTime.UtcNow)
            {
                var refreshed = await TryRefreshTokenAsync(post, ct);
                if (!refreshed)
                {
                    _logger.LogWarning(
                        "Token expired for platform {Platform} (PostId={PostId}). Emitting event.",
                        post.Platform.PlatformType, post.Id);

                    await _kafka.PublishAsync(
                        _kafkaOptions.PlatformTokenExpiredTopic,
                        new { PlatformId = post.PlatformId, PlatformType = post.Platform.PlatformType },
                        ct);
                    return;
                }
            }

            // Fetch metrics from platform API (stubbed — real implementations go here)
            var metrics = await FetchPlatformMetricsAsync(post, ct);

            analyticsDb.PostMetricSnapshots.Add(new PostMetricSnapshot
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                Views = metrics.Views,
                Likes = metrics.Likes,
                Shares = metrics.Shares,
                Comments = metrics.Comments,
                SyncedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync metrics for PostId={PostId}", post.Id);
        }
    }

    /// <summary>
    /// Attempts to refresh the platform access token using the stored refresh token.
    /// Returns true if the refresh succeeded (token updated in social_db), false otherwise.
    /// NOTE: Full token refresh requires writing back to social_db — this requires adding a
    /// writable social context or a dedicated token-refresh endpoint in the social service.
    /// Currently this is a placeholder that always returns false to trigger the Kafka event path.
    /// </summary>
    private Task<bool> TryRefreshTokenAsync(PostReadModel post, CancellationToken ct)
    {
        _logger.LogWarning(
            "Token refresh not yet implemented for platform {Platform}. " +
            "Implement by calling the social service token-refresh endpoint.",
            post.Platform.PlatformType);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Fetches metrics from the platform's analytics API using the ExternalPostId.
    /// Platform-specific implementations should be injected via IPlatformAnalyticsProvider.
    /// Currently returns zeros — wire up real API calls per platform.
    /// </summary>
    private Task<(long Views, long Likes, long Shares, long Comments)> FetchPlatformMetricsAsync(
        PostReadModel post,
        CancellationToken ct)
    {
        _logger.LogDebug(
            "Fetching metrics for PostId={PostId}, ExternalPostId={ExternalPostId}, Platform={Platform}",
            post.Id, post.ExternalPostId, post.Platform.PlatformType);

        // TODO: implement per-platform analytics API calls:
        // Instagram: GET /v19.0/{media-id}/insights?metric=impressions,reach,likes,comments,shares
        // YouTube:   GET https://youtubeanalytics.googleapis.com/v2/reports?ids=channel==MINE&metrics=views,likes,comments,shares
        // TikTok:    POST /v2/video/query/views/
        // X:         GET /2/tweets/{id}?tweet.fields=public_metrics

        return Task.FromResult((Views: 0L, Likes: 0L, Shares: 0L, Comments: 0L));
    }
}
