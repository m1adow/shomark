namespace ShoMark.Application.DTOs.Videos;

/// <summary>
/// Request to start AI highlight processing on a video.
/// </summary>
public record ProcessVideoRequest(
    string? OutputBucket,
    string? OutputPrefix);
