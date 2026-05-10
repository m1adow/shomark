using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoMark.Analytics.Infrastructure.BackgroundServices;
using ShoMark.Analytics.Infrastructure.Persistence;
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

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();

        services.AddMemoryCache();

        services.AddHostedService<MetricsSyncService>();

        return services;
    }
}
