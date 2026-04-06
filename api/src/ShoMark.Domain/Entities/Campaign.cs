using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Entities;

public class Campaign : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? FragmentId { get; set; }
    public Guid? VideoId { get; set; }
    public string? Name { get; set; }
    public TargetAudience? TargetAudience { get; set; }
    public string? Description { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    // Navigation
    public AiFragment? Fragment { get; set; }
    public Video? Video { get; set; }
    public ICollection<Post> Posts { get; set; } = [];
}
