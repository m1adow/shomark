namespace ShoMark.Domain.Entities;

public class FragmentProjection : BaseEntity
{
    public Guid FragmentId { get; set; }
    public Guid VideoId { get; set; }
    public Guid UserId { get; set; }
    public string? Description { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string? StorageKey { get; set; }
    public double ViralScore { get; set; }
    public string? Hashtags { get; set; }
    public string? ThumbnailKey { get; set; }
    public DateTime ApprovedAt { get; set; }
}
