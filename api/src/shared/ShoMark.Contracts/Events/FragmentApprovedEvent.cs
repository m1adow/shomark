namespace ShoMark.Contracts.Events;

public sealed record FragmentApprovedEvent(
    Guid FragmentId,
    Guid VideoId,
    Guid UserId,
    string? Description,
    double StartTime,
    double EndTime,
    string? StorageKey,
    double ViralScore,
    string? Hashtags,
    string? ThumbnailKey,
    DateTime ApprovedAt);
