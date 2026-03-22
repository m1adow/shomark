using ShoMark.Domain.Enums;

namespace ShoMark.Application.DTOs.Campaigns;

public record CampaignDto(
    Guid Id,
    Guid UserId,
    Guid FragmentId,
    string? Name,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateCampaignRequest(
    Guid UserId,
    Guid FragmentId,
    string? Name);

public record UpdateCampaignRequest(
    string? Name,
    CampaignStatus? Status);
