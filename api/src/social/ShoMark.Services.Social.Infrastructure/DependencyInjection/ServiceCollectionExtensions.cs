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
using ShoMark.Messaging;

namespace ShoMark.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSocialInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SocialDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();
        services.AddScoped<IFragmentProjectionRepository, FragmentProjectionRepository>();

        // Persist Data Protection keys in social_db so the Analytics service
        // can share the same key ring for decrypting platform tokens.
        // Run: dotnet ef migrations add AddDataProtectionKeys --context DataProtectionKeysContext
        services.AddDbContext<DataProtectionKeysContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        services.AddDataProtection()
            .SetApplicationName("ShoMark.Social")
            .PersistKeysToDbContext<DataProtectionKeysContext>();
        services.AddSingleton<ITokenEncryptionService, DataProtectionTokenEncryptionService>();

        services.AddMemoryCache();

        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));
        services.AddHttpClient<InstagramOAuthProvider>();
        services.AddHttpClient<TikTokOAuthProvider>();
        services.AddHttpClient<YouTubeOAuthProvider>();
        services.AddHttpClient<XOAuthProvider>();

        services.AddSingleton<IOAuthProvider>(sp => sp.GetRequiredService<InstagramOAuthProvider>());
        services.AddSingleton<IOAuthProvider>(sp => sp.GetRequiredService<TikTokOAuthProvider>());
        services.AddSingleton<IOAuthProvider>(sp => sp.GetRequiredService<YouTubeOAuthProvider>());
        services.AddSingleton<IOAuthProvider>(sp => sp.GetRequiredService<XOAuthProvider>());

        services.AddHttpClient<InstagramPublisher>();
        services.AddHttpClient<TikTokPublisher>();
        services.AddHttpClient<YouTubePublisher>();
        services.AddHttpClient<XPublisher>();

        services.AddSingleton<ISocialMediaPublisher>(sp => sp.GetRequiredService<InstagramPublisher>());
        services.AddSingleton<ISocialMediaPublisher>(sp => sp.GetRequiredService<TikTokPublisher>());
        services.AddSingleton<ISocialMediaPublisher>(sp => sp.GetRequiredService<YouTubePublisher>());
        services.AddSingleton<ISocialMediaPublisher>(sp => sp.GetRequiredService<XPublisher>());

        services.Configure<ShoMark.Messaging.KafkaOptions>(configuration.GetSection(ShoMark.Messaging.KafkaOptions.SectionName));
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<FragmentApprovedConsumer>();
        services.AddHostedService<PostSchedulerBackgroundService>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.Configure<AzureBlobOptions>(configuration.GetSection(AzureBlobOptions.SectionName));

        var storageProvider = configuration
            .GetSection(StorageOptions.SectionName)
            .GetValue<StorageProvider>(nameof(StorageOptions.Provider));

        if (storageProvider == StorageProvider.AzureBlob)
            services.AddSingleton<IStorageService, AzureBlobStorageService>();
        else
            services.AddSingleton<IStorageService, MinioStorageService>();

        return services;
    }

    public static IServiceCollection AddSocialApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IPostPublishingService, PostPublishingService>();

        return services;
    }
}
