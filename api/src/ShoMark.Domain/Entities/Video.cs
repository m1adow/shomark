namespace ShoMark.Domain.Entities;

public class Video : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string MinioKey { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    public double? DurationSeconds { get; set; }
    public long? FileSize { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<AiFragment> Fragments { get; set; } = [];
}
