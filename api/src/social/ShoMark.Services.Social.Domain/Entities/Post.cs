using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Entities;

public class Post : BaseEntity
{
    public Guid FragmentId { get; set; }
    public Guid PlatformId { get; set; }
    public Guid? CampaignId { get; set; }
    public string? FragmentDescription { get; set; }
    public double? FragmentStartTime { get; set; }
    public double? FragmentEndTime { get; set; }
    public string? FragmentStorageKey { get; set; }
    public string? FragmentThumbnailKey { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ExternalUrl { get; set; }
    public string? ExternalPostId { get; set; }
    public PostStatus Status { get; set; } = PostStatus.Draft;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Navigation
    public Platform Platform { get; set; } = null!;
    public Analytics? Analytics { get; set; }
}
