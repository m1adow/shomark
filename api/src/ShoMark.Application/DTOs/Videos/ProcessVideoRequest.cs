using ShoMark.Domain.Enums;

namespace ShoMark.Application.DTOs.Videos;

/// <summary>
/// Request to start AI highlight processing on a video.
/// </summary>
public record ProcessVideoRequest(
    string? OutputBucket,
    string? OutputPrefix,
    TargetAudience? TargetAudience,
    string? Description);
