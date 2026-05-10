namespace ShoMark.Application.DTOs.Videos;

/// <summary>
/// A time range (in seconds) that the worker should avoid when selecting new highlights.
/// Derived server-side from existing AI fragments for the same video.
/// </summary>
public record ExcludeRange(double Start, double End);
