using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Services;
using ShoMark.Domain.Enums;
using ShoMark.Domain.Interfaces;
using ShoMark.Infrastructure.Data;
using ShoMark.Infrastructure.Messaging;
using ShoMark.Infrastructure.OAuth;
using ShoMark.Infrastructure.Publishing;
using ShoMark.Infrastructure.Repositories;
using ShoMark.Infrastructure.Security;
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
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // HttpContext accessor (required by CurrentUserAccessor)
        services.AddHttpContextAccessor();

        // Data Protection (token encryption)
        services.AddDataProtection()
            .SetApplicationName("ShoMark")
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(AppContext.BaseDirectory, "DataProtection-Keys")));
        services.AddSingleton<ITokenEncryptionService, DataProtectionTokenEncryptionService>();

        // Memory cache (for OAuth state)
        services.AddMemoryCache();

        // OAuth providers
        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));
        services.AddHttpClient<InstagramOAuthProvider>();
        services.AddHttpClient<TikTokOAuthProvider>();
        services.AddHttpClient<YouTubeOAuthProvider>();
        services.AddHttpClient<XOAuthProvider>();

        services.AddSingleton<IOAuthProvider>(sp =>
            sp.GetRequiredService<InstagramOAuthProvider>());
        services.AddSingleton<IOAuthProvider>(sp =>
            sp.GetRequiredService<TikTokOAuthProvider>());
        services.AddSingleton<IOAuthProvider>(sp =>
            sp.GetRequiredService<YouTubeOAuthProvider>());
        services.AddSingleton<IOAuthProvider>(sp =>
            sp.GetRequiredService<XOAuthProvider>());

        // Social media publishers
        services.AddHttpClient<InstagramPublisher>();
        services.AddHttpClient<TikTokPublisher>();
        services.AddHttpClient<YouTubePublisher>();
        services.AddHttpClient<XPublisher>();

        services.AddSingleton<ISocialMediaPublisher>(sp =>
            sp.GetRequiredService<InstagramPublisher>());
        services.AddSingleton<ISocialMediaPublisher>(sp =>
            sp.GetRequiredService<TikTokPublisher>());
        services.AddSingleton<ISocialMediaPublisher>(sp =>
            sp.GetRequiredService<YouTubePublisher>());
        services.AddSingleton<ISocialMediaPublisher>(sp =>
            sp.GetRequiredService<XPublisher>());

        // Kafka
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IVideoProcessingProducer, KafkaVideoProcessingProducer>();
        services.AddSingleton<IVideoProcessingNotifier, VideoProcessingNotifier>();
        services.AddSingleton<INotificationSseNotifier, NotificationSseNotifier>();
        services.AddHostedService<KafkaCompletionConsumer>();

        // Post publishing pipeline
        services.AddSingleton<IPostPublishingProducer, KafkaPostPublishingProducer>();
        services.AddHostedService<PostSchedulerBackgroundService>();
        services.AddHostedService<KafkaPostPublishingConsumer>();

        // Object Storage (provider-switchable via Storage:Provider config)
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.Configure<AzureBlobOptions>(configuration.GetSection(AzureBlobOptions.SectionName));
        services.Configure<VideoOptions>(configuration.GetSection(VideoOptions.SectionName));

        var storageProvider = configuration
            .GetSection(StorageOptions.SectionName)
            .GetValue<StorageProvider>(nameof(StorageOptions.Provider));

        if (storageProvider == StorageProvider.AzureBlob)
            services.AddSingleton<IStorageService, AzureBlobStorageService>();
        else
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

                // Allow SSE (EventSource) to pass JWT via query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var path = context.Request.Path;

                        var isSseEndpoint =
                            (path.StartsWithSegments("/api/videos")
                             && path.Value?.EndsWith("/events") == true)
                            || path.StartsWithSegments("/api/notifications/stream");

                        if (isSseEndpoint
                            && context.Request.Query.TryGetValue("access_token", out var token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IAiFragmentService, AiFragmentService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IPostPublishingService, PostPublishingService>();

        return services;
    }
}
