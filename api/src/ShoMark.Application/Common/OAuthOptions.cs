namespace ShoMark.Application.Common;

public class OAuthOptions
{
    public const string SectionName = "OAuth";

    public OAuthPlatformConfig Instagram { get; set; } = new();
    public OAuthPlatformConfig TikTok { get; set; } = new();
    public OAuthPlatformConfig YouTube { get; set; } = new();
    public OAuthPlatformConfig X { get; set; } = new();
}

public class OAuthPlatformConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
}
