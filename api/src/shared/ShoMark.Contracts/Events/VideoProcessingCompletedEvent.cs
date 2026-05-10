namespace ShoMark.Contracts.Events;

public sealed record VideoProcessingCompletedEvent(
    Guid VideoId,
    Guid UserId,
    string? Title,
    int HighlightCount,
    DateTime CompletedAt);