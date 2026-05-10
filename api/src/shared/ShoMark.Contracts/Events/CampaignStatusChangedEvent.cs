namespace ShoMark.Contracts.Events;

public sealed record CampaignStatusChangedEvent(
    Guid CampaignId,
    Guid UserId,
    string? Name,
    string PreviousStatus,
    string NewStatus,
    DateTime ChangedAt);
