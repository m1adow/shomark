namespace ShoMark.Application.DTOs.Analytics;

public record AnalyticsDto(
    Guid Id,
    Guid PostId,
    long Views,
    long Likes,
    long Shares,
    long Comments,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpdateAnalyticsRequest(
    long Views,
    long Likes,
    long Shares,
    long Comments);
