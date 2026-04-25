namespace ShoMark.Application.DTOs.Videos;

public record VideoDto(
    Guid Id,
    string Title,
    string StorageKey,
    string? OriginalFileName,
    double? DurationSeconds,
    long? FileSize,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateVideoRequest(
    string Title,
    string StorageKey,
    string? OriginalFileName,
    double? DurationSeconds,
    long? FileSize);

public record UpdateVideoRequest(
    string Title,
    string? OriginalFileName,
    double? DurationSeconds,
    long? FileSize);

public record VideoWithFragmentsDto(
    Guid Id,
    string Title,
    string StorageKey,
    string? OriginalFileName,
    double? DurationSeconds,
    long? FileSize,
    DateTime CreatedAt,
    IReadOnlyList<FragmentSummaryDto> Fragments);

public record FragmentSummaryDto(
    Guid Id,
    string? Description,
    double StartTime,
    double EndTime,
    string? StorageKey,
    double? ViralScore,
    string? Hashtags,
    string? ThumbnailKey,
    bool IsApproved);
