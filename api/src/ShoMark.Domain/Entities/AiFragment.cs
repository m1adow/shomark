namespace ShoMark.Domain.Entities;

public class AiFragment : BaseEntity
{
    public Guid VideoId { get; set; }
    public string? Description { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string? StorageKey { get; set; }
    public double? ViralScore { get; set; }
    public string? Hashtags { get; set; }
    public string? ThumbnailKey { get; set; }
    public bool IsApproved { get; set; }

    // Navigation
    public Video Video { get; set; } = null!;
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Campaign> Campaigns { get; set; } = [];
}
