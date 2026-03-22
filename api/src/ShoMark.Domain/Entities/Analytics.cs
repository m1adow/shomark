namespace ShoMark.Domain.Entities;

public class Analytics : BaseEntity
{
    public Guid PostId { get; set; }
    public long Views { get; set; }
    public long Likes { get; set; }
    public long Shares { get; set; }
    public long Comments { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    // Navigation
    public Post Post { get; set; } = null!;
}
