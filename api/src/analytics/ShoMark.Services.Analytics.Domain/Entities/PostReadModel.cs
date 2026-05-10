namespace ShoMark.Analytics.Domain.Entities;

/// <summary>Read-only projection of social_db.posts used by the Analytics service.</summary>
public class PostReadModel
{
    public Guid Id { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid PlatformId { get; set; }
    public string? Title { get; set; }
    public string? ExternalPostId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }

    // Navigation
    public PlatformReadModel Platform { get; set; } = null!;
}
