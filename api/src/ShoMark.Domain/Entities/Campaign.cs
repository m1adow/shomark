using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Entities;

public class Campaign : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FragmentId { get; set; }
    public string? Name { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    // Navigation
    public User User { get; set; } = null!;
    public AiFragment Fragment { get; set; } = null!;
}
