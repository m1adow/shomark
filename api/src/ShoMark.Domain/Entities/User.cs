namespace ShoMark.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Navigation
    public ICollection<Platform> Platforms { get; set; } = [];
    public ICollection<Campaign> Campaigns { get; set; } = [];
}
