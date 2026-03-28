using ShoMark.Domain.Enums;

namespace ShoMark.Application.DTOs.Campaigns;

public record CampaignDto(
    Guid Id,
    Guid UserId,
    Guid? FragmentId,
    Guid? VideoId,
    string? Name,
    string? TargetAudience,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateCampaignRequest(
    Guid UserId,
    Guid? FragmentId,
    Guid? VideoId,
    string? Name,
    TargetAudience? TargetAudience,
    string? Description);

public record UpdateCampaignRequest(
    string? Name,
    CampaignStatus? Status,
    TargetAudience? TargetAudience,
    string? Description,
    Guid? VideoId,
    Guid? FragmentId);
