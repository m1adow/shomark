namespace ShoMark.Domain.Entities;

public class FragmentTag
{
    public Guid FragmentId { get; set; }
    public Guid TagId { get; set; }

    // Navigation
    public AiFragment Fragment { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
