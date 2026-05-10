using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Services;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;
using ShoMark.Infrastructure.Messaging;
using ShoMark.Infrastructure.Repositories;
using ShoMark.Messaging;

namespace ShoMark.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddSingleton<INotificationSseNotifier, NotificationSseNotifier>();

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddHostedService<DomainEventNotificationConsumer>();

        return services;
    }

    public static IServiceCollection AddNotificationApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
