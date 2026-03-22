using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Entities;

public class Platform : BaseEntity
{
    public Guid UserId { get; set; }
    public PlatformType PlatformType { get; set; }
    public string? AccountName { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Post> Posts { get; set; } = [];
}
