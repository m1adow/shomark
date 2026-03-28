using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Entities;

public class Post : BaseEntity
{
    public Guid FragmentId { get; set; }
    public Guid PlatformId { get; set; }
    public Guid? CampaignId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ExternalUrl { get; set; }
    public PostStatus Status { get; set; } = PostStatus.Draft;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Navigation
    public AiFragment Fragment { get; set; } = null!;
    public Platform Platform { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public Analytics? Analytics { get; set; }
}
