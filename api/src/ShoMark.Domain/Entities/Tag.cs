namespace ShoMark.Domain.Entities;

public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    // Navigation
    public ICollection<FragmentTag> FragmentTags { get; set; } = [];
}
