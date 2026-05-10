namespace ShoMark.Contracts.Events;

public sealed record PostPublishedEvent(
    Guid PostId,
    Guid UserId,
    Guid PlatformId,
    Guid FragmentId,
    Guid? CampaignId,
    string? Title,
    string? ExternalUrl,
    string PlatformType,
    DateTime PublishedAt);
