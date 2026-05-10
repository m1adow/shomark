namespace ShoMark.Analytics.Api.DTOs;

public record CampaignAnalyticsDto(
    AnalyticsSummaryDto Summary,
    IReadOnlyList<PlatformAnalyticsDto> Platforms,
    IReadOnlyList<PostAnalyticsDto> Posts,
    IReadOnlyList<TrendPointDto> Trend
);

public record AnalyticsSummaryDto(
    long TotalViews,
    long TotalLikes,
    long TotalShares,
    long TotalComments,
    double EngagementRate,
    int TotalPosts,
    int PublishedPosts,
    int ScheduledPosts,
    int FailedPosts
);

public record PlatformAnalyticsDto(
    string Platform,
    long Views,
    long Likes,
    long Shares,
    long Comments,
    double EngagementRate,
    int PostCount
);

public record PostAnalyticsDto(
    Guid PostId,
    string? Title,
    string Platform,
    string Status,
    DateTime? PublishedAt,
    long Views,
    long Likes,
    long Shares,
    long Comments,
    double EngagementRate
);

public record TrendPointDto(
    string Date,
    long Views,
    long Likes,
    long Shares,
    long Comments,
    double EngagementRate
);
