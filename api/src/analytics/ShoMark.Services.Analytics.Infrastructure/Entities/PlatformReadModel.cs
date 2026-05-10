namespace ShoMark.Analytics.Infrastructure.Entities;

/// <summary>Read-only projection of social_db.platforms used by the Analytics service.</summary>
public class PlatformReadModel
{
    public Guid Id { get; set; }
    public string PlatformType { get; set; } = null!;
    public string? AccountName { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }

    // Navigation
    public ICollection<PostReadModel> Posts { get; set; } = [];
}
