namespace ShoMark.Analytics.Domain.Entities;

/// <summary>Read-only projection of campaigns_db.campaigns used by the Analytics service.</summary>
public class CampaignReadModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = null!;
}
