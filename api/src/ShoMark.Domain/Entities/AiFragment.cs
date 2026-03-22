namespace ShoMark.Domain.Entities;

public class AiFragment : BaseEntity
{
    public Guid VideoId { get; set; }
    public string? Description { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string? MinioKey { get; set; }

    // Navigation
    public Video Video { get; set; } = null!;
    public ICollection<FragmentTag> FragmentTags { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Campaign> Campaigns { get; set; } = [];
}
