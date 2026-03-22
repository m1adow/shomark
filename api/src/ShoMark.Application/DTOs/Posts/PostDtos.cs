using ShoMark.Domain.Enums;

namespace ShoMark.Application.DTOs.Posts;

public record PostDto(
    Guid Id,
    Guid FragmentId,
    Guid PlatformId,
    string? Title,
    string? Content,
    string? ExternalUrl,
    string Status,
    DateTime? ScheduledAt,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreatePostRequest(
    Guid FragmentId,
    Guid PlatformId,
    string? Title,
    string? Content,
    DateTime? ScheduledAt);

public record UpdatePostRequest(
    string? Title,
    string? Content,
    string? ExternalUrl,
    PostStatus? Status,
    DateTime? ScheduledAt,
    DateTime? PublishedAt);

public record PostWithAnalyticsDto(
    Guid Id,
    Guid FragmentId,
    Guid PlatformId,
    string? Title,
    string? Content,
    string? ExternalUrl,
    string Status,
    DateTime? ScheduledAt,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    AnalyticsSummaryDto? Analytics);

public record AnalyticsSummaryDto(
    long Views,
    long Likes,
    long Shares,
    long Comments,
    DateTime? LastSyncedAt);
