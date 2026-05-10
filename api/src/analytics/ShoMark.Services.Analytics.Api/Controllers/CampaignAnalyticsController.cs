using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShoMark.Analytics.Api.DTOs;
using ShoMark.Analytics.Api.Services;
using ShoMark.Analytics.Infrastructure.Persistence;

namespace ShoMark.Analytics.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public class CampaignAnalyticsController : ControllerBase
{
    private readonly AnalyticsDbContext _analyticsDb;
    private readonly SocialReadContext _socialDb;
    private readonly CampaignsReadContext _campaignsDb;
    private readonly TrustedHeaderCurrentUserAccessor _userAccessor;
    private readonly IMemoryCache _cache;

    public CampaignAnalyticsController(
        AnalyticsDbContext analyticsDb,
        SocialReadContext socialDb,
        CampaignsReadContext campaignsDb,
        TrustedHeaderCurrentUserAccessor userAccessor,
        IMemoryCache cache)
    {
        _analyticsDb = analyticsDb;
        _socialDb = socialDb;
        _campaignsDb = campaignsDb;
        _userAccessor = userAccessor;
        _cache = cache;
    }

    [HttpGet("campaigns/{id:guid}")]
    public async Task<IActionResult> GetCampaignAnalytics(Guid id, CancellationToken ct)
    {
        var userId = _userAccessor.GetUserId();
        var cacheKey = $"analytics:{userId}:{id}";

        if (_cache.TryGetValue(cacheKey, out CampaignAnalyticsDto? cached))
            return Ok(cached);

        var campaign = await _campaignsDb.Campaigns
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (campaign is null)
            return NotFound(new { Error = "Campaign not found", ErrorCode = "CAMPAIGN_NOT_FOUND" });

        if (campaign.UserId != userId)
            return StatusCode(StatusCodes.Status403Forbidden, new { Error = "Access denied", ErrorCode = "FORBIDDEN" });

        var posts = await _socialDb.Posts
            .Include(p => p.Platform)
            .Where(p => p.CampaignId == id)
            .ToListAsync(ct);

        var postIds = posts.Select(p => p.Id).ToList();

        // Latest snapshot per post (most recent SyncedAt)
        var snapshots = await _analyticsDb.PostMetricSnapshots
            .Where(s => postIds.Contains(s.PostId))
            .GroupBy(s => s.PostId)
            .Select(g => g.OrderByDescending(s => s.SyncedAt).First())
            .ToListAsync(ct);

        var snapshotByPost = snapshots.ToDictionary(s => s.PostId);

        // Summary
        long totalViews = snapshots.Sum(s => s.Views);
        long totalLikes = snapshots.Sum(s => s.Likes);
        long totalShares = snapshots.Sum(s => s.Shares);
        long totalComments = snapshots.Sum(s => s.Comments);
        long totalEngageable = totalViews;
        double engagementRate = totalEngageable > 0
            ? Math.Round((double)(totalLikes + totalShares + totalComments) / totalEngageable * 100, 2)
            : 0;

        var summary = new AnalyticsSummaryDto(
            TotalViews: totalViews,
            TotalLikes: totalLikes,
            TotalShares: totalShares,
            TotalComments: totalComments,
            EngagementRate: engagementRate,
            TotalPosts: posts.Count,
            PublishedPosts: posts.Count(p => p.Status == "Published"),
            ScheduledPosts: posts.Count(p => p.Status == "Scheduled"),
            FailedPosts: posts.Count(p => p.Status == "Failed")
        );

        // Per-platform breakdown
        var platforms = posts
            .GroupBy(p => p.Platform.PlatformType)
            .Select(g =>
            {
                var platformSnapshots = g
                    .Where(p => snapshotByPost.ContainsKey(p.Id))
                    .Select(p => snapshotByPost[p.Id])
                    .ToList();

                long pViews = platformSnapshots.Sum(s => s.Views);
                long pLikes = platformSnapshots.Sum(s => s.Likes);
                long pShares = platformSnapshots.Sum(s => s.Shares);
                long pComments = platformSnapshots.Sum(s => s.Comments);
                double pEngagement = pViews > 0
                    ? Math.Round((double)(pLikes + pShares + pComments) / pViews * 100, 2)
                    : 0;

                return new PlatformAnalyticsDto(
                    Platform: g.Key,
                    Views: pViews,
                    Likes: pLikes,
                    Shares: pShares,
                    Comments: pComments,
                    EngagementRate: pEngagement,
                    PostCount: g.Count()
                );
            })
            .OrderBy(p => p.Platform)
            .ToList();

        // Per-post breakdown
        var postDtos = posts.Select(p =>
        {
            var snap = snapshotByPost.GetValueOrDefault(p.Id);
            long pViews = snap?.Views ?? 0;
            long pLikes = snap?.Likes ?? 0;
            long pShares = snap?.Shares ?? 0;
            long pComments = snap?.Comments ?? 0;
            double pEng = pViews > 0
                ? Math.Round((double)(pLikes + pShares + pComments) / pViews * 100, 2)
                : 0;

            return new PostAnalyticsDto(
                PostId: p.Id,
                Title: p.Title,
                Platform: p.Platform.PlatformType,
                Status: p.Status,
                PublishedAt: p.PublishedAt,
                Views: pViews,
                Likes: pLikes,
                Shares: pShares,
                Comments: pComments,
                EngagementRate: pEng
            );
        }).ToList();

        // Trend: group all snapshots by date
        var allSnapshots = await _analyticsDb.PostMetricSnapshots
            .Where(s => postIds.Contains(s.PostId))
            .ToListAsync(ct);

        var trend = allSnapshots
            .GroupBy(s => s.SyncedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                long tViews = g.Sum(s => s.Views);
                long tLikes = g.Sum(s => s.Likes);
                long tShares = g.Sum(s => s.Shares);
                long tComments = g.Sum(s => s.Comments);
                double tEng = tViews > 0
                    ? Math.Round((double)(tLikes + tShares + tComments) / tViews * 100, 2)
                    : 0;

                return new TrendPointDto(
                    Date: g.Key.ToString("yyyy-MM-dd"),
                    Views: tViews,
                    Likes: tLikes,
                    Shares: tShares,
                    Comments: tComments,
                    EngagementRate: tEng
                );
            })
            .ToList();

        var result = new CampaignAnalyticsDto(summary, platforms, postDtos, trend);

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));

        return Ok(result);
    }
}
