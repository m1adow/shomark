namespace ShoMark.Analytics.Infrastructure.Providers;

public interface IPlatformAnalyticsProvider
{
    /// <summary>
    /// The PlatformType string this provider handles (e.g. "Instagram", "YouTube").
    /// Must match the value stored in social_db.platforms.platform_type.
    /// </summary>
    string PlatformType { get; }

    /// <summary>
    /// Fetches the latest metrics for a published post from the platform's analytics API.
    /// Throws on API errors; the caller (MetricsSyncService) handles and logs failures.
    /// </summary>
    Task<PlatformMetrics> FetchAsync(string externalPostId, string accessToken, CancellationToken ct);
}
