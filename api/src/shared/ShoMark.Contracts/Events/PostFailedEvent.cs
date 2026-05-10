namespace ShoMark.Contracts.Events;

public sealed record PostFailedEvent(
    Guid PostId,
    Guid UserId,
    Guid PlatformId,
    Guid FragmentId,
    Guid? CampaignId,
    string? Title,
    string PlatformType,
    string ErrorMessage,
    DateTime FailedAt);
