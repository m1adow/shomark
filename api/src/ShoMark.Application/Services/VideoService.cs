using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Videos;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class VideoService : IVideoService
{
    private readonly IVideoRepository _videoRepository;
    private readonly IVideoProcessingProducer _processingProducer;
    private readonly IStorageService _storageService;
    private readonly MinioOptions _minioOptions;

    public VideoService(
        IVideoRepository videoRepository,
        IVideoProcessingProducer processingProducer,
        IStorageService storageService,
        IOptions<MinioOptions> minioOptions)
    {
        _videoRepository = videoRepository;
        _processingProducer = processingProducer;
        _storageService = storageService;
        _minioOptions = minioOptions.Value;
    }

    public async Task<Result<VideoDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<VideoDto>.Failure("Video not found", "NOT_FOUND");

        return Result<VideoDto>.Success(MapToDto(video));
    }

    public async Task<Result<IReadOnlyList<VideoDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var videos = await _videoRepository.GetActiveVideosAsync(ct);
        return Result<IReadOnlyList<VideoDto>>.Success(
            videos.Select(MapToDto).ToList());
    }

    public async Task<Result<VideoWithFragmentsDto>> GetWithFragmentsAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetWithFragmentsAsync(id, ct);
        if (video is null)
            return Result<VideoWithFragmentsDto>.Failure("Video not found", "NOT_FOUND");

        var dto = new VideoWithFragmentsDto(
            video.Id, video.Title, video.MinioKey, video.OriginalFileName,
            video.DurationSeconds, video.FileSize, video.CreatedAt,
            video.Fragments.Select(f => new FragmentSummaryDto(
                f.Id, f.Description, f.StartTime, f.EndTime,
                f.MinioKey, f.ViralScore, f.Hashtags, f.ThumbnailKey, f.IsApproved)).ToList());

        return Result<VideoWithFragmentsDto>.Success(dto);
    }

    public async Task<Result<VideoDto>> CreateAsync(CreateVideoRequest request, CancellationToken ct = default)
    {
        var existing = await _videoRepository.GetByMinioKeyAsync(request.MinioKey, ct);
        if (existing is not null)
            return Result<VideoDto>.Failure("A video with this MinIO key already exists", "DUPLICATE");

        var video = new Video
        {
            Title = request.Title,
            MinioKey = request.MinioKey,
            OriginalFileName = request.OriginalFileName,
            DurationSeconds = request.DurationSeconds,
            FileSize = request.FileSize
        };

        var created = await _videoRepository.AddAsync(video, ct);
        return Result<VideoDto>.Success(MapToDto(created));
    }

    public async Task<Result<VideoDto>> UpdateAsync(Guid id, UpdateVideoRequest request, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<VideoDto>.Failure("Video not found", "NOT_FOUND");

        video.Title = request.Title;
        video.OriginalFileName = request.OriginalFileName;
        video.DurationSeconds = request.DurationSeconds;
        video.FileSize = request.FileSize;

        await _videoRepository.UpdateAsync(video, ct);
        return Result<VideoDto>.Success(MapToDto(video));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<bool>.Failure("Video not found", "NOT_FOUND");

        // Soft delete
        video.DeletedAt = DateTime.UtcNow;
        await _videoRepository.UpdateAsync(video, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ProcessVideoAsync(Guid id, ProcessVideoRequest request, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<bool>.Failure("Video not found", "NOT_FOUND");

        // Parse bucket and key from the MinIO key (format: "bucket/key" or just "key")
        var parts = video.MinioKey.Split('/', 2);
        var videoBucket = parts.Length > 1 ? parts[0] : "videos";
        var videoKey = parts.Length > 1 ? parts[1] : video.MinioKey;

        var outputBucket = request.OutputBucket ?? "highlights";
        var outputPrefix = request.OutputPrefix ?? $"{video.Id}/";

        await _processingProducer.SendProcessingRequestAsync(
            videoBucket, videoKey, outputBucket, outputPrefix,
            request.TargetAudience, request.Description, ct);

        return Result<bool>.Success(true);
    }

    //TODO: make all map methods with static extension methods in a separate class
    private static VideoDto MapToDto(Video v) => new(
        v.Id, v.Title, v.MinioKey, v.OriginalFileName,
        v.DurationSeconds, v.FileSize, v.CreatedAt, v.UpdatedAt);

    public async Task<Result<VideoDto>> UploadAsync(Stream fileStream, string fileName, long fileSize, string contentType, CancellationToken ct = default)
    {
        var videoId = Guid.NewGuid();
        var extension = Path.GetExtension(fileName);
        var key = $"{videoId}{extension}";
        var bucket = _minioOptions.VideoBucket;

        await _storageService.UploadFileAsync(fileStream, bucket, key, contentType, ct);

        var video = new Video
        {
            Id = videoId,
            Title = Path.GetFileNameWithoutExtension(fileName),
            MinioKey = $"{bucket}/{key}",
            OriginalFileName = fileName,
            FileSize = fileSize
        };

        var created = await _videoRepository.AddAsync(video, ct);
        return Result<VideoDto>.Success(MapToDto(created));
    }

    public async Task<Result<string>> GetVideoUrlAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<string>.Failure("Video not found", "NOT_FOUND");

        var parts = video.MinioKey.Split('/', 2);
        var bucket = parts.Length > 1 ? parts[0] : _minioOptions.VideoBucket;
        var key = parts.Length > 1 ? parts[1] : video.MinioKey;

        var url = await _storageService.GetPresignedUrlAsync(bucket, key, 3600, ct);
        return Result<string>.Success(url);
    }
}
