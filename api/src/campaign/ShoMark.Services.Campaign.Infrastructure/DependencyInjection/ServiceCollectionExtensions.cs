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
using ShoMark.Infrastructure.Repositories;
using ShoMark.Infrastructure.Storage;
using ShoMark.Messaging;

namespace ShoMark.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCampaignInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CampaignDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<IAiFragmentRepository, AiFragmentRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();

        services.Configure<ShoMark.Application.Common.KafkaOptions>(configuration.GetSection(ShoMark.Application.Common.KafkaOptions.SectionName));
        services.Configure<ShoMark.Messaging.KafkaOptions>(configuration.GetSection(ShoMark.Messaging.KafkaOptions.SectionName));
        services.AddSingleton<IVideoProcessingProducer, KafkaVideoProcessingProducer>();
        services.AddSingleton<IVideoProcessingNotifier, VideoProcessingNotifier>();
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<KafkaCompletionConsumer>();

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

        return services;
    }

    public static IServiceCollection AddCampaignApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IAiFragmentService, AiFragmentService>();
        services.AddScoped<ICampaignService, CampaignService>();

        return services;
    }
}
