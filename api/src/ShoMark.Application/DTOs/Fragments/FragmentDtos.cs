namespace ShoMark.Application.DTOs.Fragments;

public record AiFragmentDto(
    Guid Id,
    Guid VideoId,
    string? Description,
    double StartTime,
    double EndTime,
    string? MinioKey,
    double? ViralScore,
    string? Hashtags,
    string? ThumbnailKey,
    bool IsApproved,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateAiFragmentRequest(
    Guid VideoId,
    string? Description,
    double StartTime,
    double EndTime,
    string? MinioKey,
    double? ViralScore,
    string? Hashtags,
    string? ThumbnailKey,
    List<string>? TagIds);

public record UpdateAiFragmentRequest(
    string? Description,
    double StartTime,
    double EndTime,
    string? MinioKey,
    double? ViralScore,
    string? Hashtags,
    bool? IsApproved);

public record AiFragmentDetailDto(
    Guid Id,
    Guid VideoId,
    string? Description,
    double StartTime,
    double EndTime,
    string? MinioKey,
    double? ViralScore,
    string? Hashtags,
    string? ThumbnailKey,
    bool IsApproved,
    DateTime CreatedAt,
    IReadOnlyList<TagSummaryDto> Tags,
    IReadOnlyList<PostSummaryDto> Posts);

public record TagSummaryDto(Guid Id, string Name, string Slug);

public record PostSummaryDto(Guid Id, string? Title, string Status);
