using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoMark.Analytics.Infrastructure.BackgroundServices;
using ShoMark.Analytics.Infrastructure.Persistence;
using ShoMark.Analytics.Infrastructure.Providers;
using ShoMark.Messaging;

namespace ShoMark.Analytics.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AnalyticsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("AnalyticsConnection")));

        services.AddDbContext<SocialReadContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("SocialConnection"))
                   .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddDbContext<CampaignsReadContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CampaignsConnection"))
                   .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // Shared Data Protection key ring — must use the same application name as the
        // Social service so tokens encrypted there can be decrypted here.
        services.AddDataProtection()
            .SetApplicationName("ShoMark.Social")
            .PersistKeysToFileSystem(new DirectoryInfo("/data-protection-keys"));

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();

        services.AddMemoryCache();

        // Platform analytics providers
        services.AddHttpClient<InstagramAnalyticsProvider>();
        services.AddHttpClient<YouTubeAnalyticsProvider>();
        services.AddHttpClient<TikTokAnalyticsProvider>();
        services.AddHttpClient<XAnalyticsProvider>();

        services.AddSingleton<IPlatformAnalyticsProvider>(sp => sp.GetRequiredService<InstagramAnalyticsProvider>());
        services.AddSingleton<IPlatformAnalyticsProvider>(sp => sp.GetRequiredService<YouTubeAnalyticsProvider>());
        services.AddSingleton<IPlatformAnalyticsProvider>(sp => sp.GetRequiredService<TikTokAnalyticsProvider>());
        services.AddSingleton<IPlatformAnalyticsProvider>(sp => sp.GetRequiredService<XAnalyticsProvider>());

        services.AddHostedService<MetricsSyncService>();

        return services;
    }
}
