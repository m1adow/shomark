namespace ShoMark.Analytics.Domain.Entities;

public class PostMetricSnapshot
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public long Views { get; set; }
    public long Likes { get; set; }
    public long Shares { get; set; }
    public long Comments { get; set; }
    public DateTime SyncedAt { get; set; }
}
