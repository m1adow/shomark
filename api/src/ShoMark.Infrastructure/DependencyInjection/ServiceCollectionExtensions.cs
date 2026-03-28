using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Services;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;
using ShoMark.Infrastructure.Messaging;
using ShoMark.Infrastructure.Repositories;
using ShoMark.Infrastructure.Storage;

namespace ShoMark.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ShoMarkDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(ShoMarkDbContext).Assembly.FullName)));

        // Repositories
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<IAiFragmentRepository, AiFragmentRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();

        // Kafka
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IVideoProcessingProducer, KafkaVideoProcessingProducer>();
        services.AddHostedService<KafkaCompletionConsumer>();

        // MinIO Storage
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.AddSingleton<IStorageService, MinioStorageService>();

        // Authentication (Keycloak)
        var keycloakOptions = configuration
            .GetSection(KeycloakOptions.SectionName)
            .Get<KeycloakOptions>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

                options.TokenValidationParameters.ValidIssuer =
                    string.IsNullOrEmpty(keycloakOptions.ValidIssuer)
                        ? keycloakOptions.Authority
                        : keycloakOptions.ValidIssuer;

                options.TokenValidationParameters.ValidateAudience = false;
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IAiFragmentService, AiFragmentService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<ICampaignService, CampaignService>();

        return services;
    }
}
