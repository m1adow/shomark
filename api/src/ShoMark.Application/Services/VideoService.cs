using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Videos;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class VideoService : IVideoService
{
    private readonly IVideoRepository _videoRepository;
    private readonly IVideoProcessingProducer _processingProducer;
    private readonly IStorageService _storageService;
    private readonly StorageOptions _storageOptions;

    public VideoService(
        IVideoRepository videoRepository,
        IVideoProcessingProducer processingProducer,
        IStorageService storageService,
        IOptions<StorageOptions> storageOptions)
    {
        _videoRepository = videoRepository;
        _processingProducer = processingProducer;
        _storageService = storageService;
        _storageOptions = storageOptions.Value;
    }

    public async Task<Result<VideoDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<VideoDto>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);

        return Result<VideoDto>.Success(video.ToDto());
    }

    public async Task<Result<IReadOnlyList<VideoDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var videos = await _videoRepository.GetActiveVideosAsync(ct);
        return Result<IReadOnlyList<VideoDto>>.Success(
            videos.Select(v => v.ToDto()).ToList());
    }

    public async Task<Result<VideoWithFragmentsDto>> GetWithFragmentsAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetWithFragmentsAsync(id, ct);
        if (video is null)
            return Result<VideoWithFragmentsDto>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);

        var dto = new VideoWithFragmentsDto(
            video.Id, video.Title, video.StorageKey, video.OriginalFileName,
            video.DurationSeconds, video.FileSize, video.CreatedAt,
            video.Fragments.Select(f => new FragmentSummaryDto(
                f.Id, f.Description, f.StartTime, f.EndTime,
                f.StorageKey, f.ViralScore, f.Hashtags, f.ThumbnailKey, f.IsApproved)).ToList());

        return Result<VideoWithFragmentsDto>.Success(dto);
    }

    public async Task<Result<VideoDto>> CreateAsync(CreateVideoRequest request, CancellationToken ct = default)
    {
        var existing = await _videoRepository.GetByStorageKeyAsync(request.StorageKey, ct);
        if (existing is not null)
            return Result<VideoDto>.Failure(Constants.Errors.Messages.DuplicateVideo, Constants.Errors.Codes.Duplicate);

        var video = new Video
        {
            Title = request.Title,
            StorageKey = request.StorageKey,
            OriginalFileName = request.OriginalFileName,
            DurationSeconds = request.DurationSeconds,
            FileSize = request.FileSize
        };

        var created = await _videoRepository.AddAsync(video, ct);
        return Result<VideoDto>.Success(created.ToDto());
    }

    public async Task<Result<VideoDto>> UpdateAsync(Guid id, UpdateVideoRequest request, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<VideoDto>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);

        video.Title = request.Title;
        video.OriginalFileName = request.OriginalFileName;
        video.DurationSeconds = request.DurationSeconds;
        video.FileSize = request.FileSize;

        await _videoRepository.UpdateAsync(video, ct);
        return Result<VideoDto>.Success(video.ToDto());
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<bool>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);

        // Soft delete
        video.DeletedAt = DateTime.UtcNow;
        await _videoRepository.UpdateAsync(video, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ProcessVideoAsync(Guid id, ProcessVideoRequest request, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<bool>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);

        // Parse bucket and key from the storage key (format: "bucket/key" or just "key")
        var parts = video.StorageKey.Split('/', 2);
        var videoBucket = parts.Length > 1 ? parts[0] : Constants.Storage.VideosBucket;
        var videoKey = parts.Length > 1 ? parts[1] : video.StorageKey;

        var outputBucket = request.OutputBucket ?? Constants.Storage.HighlightsBucket;
        var outputPrefix = request.OutputPrefix ?? $"{video.Id}/";

        await _processingProducer.SendProcessingRequestAsync(
            videoBucket, videoKey, outputBucket, outputPrefix,
            request.TargetAudience, request.Description, ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<VideoDto>> UploadAsync(Stream fileStream, string fileName, long fileSize, string contentType, CancellationToken ct = default)
    {
        var videoId = Guid.NewGuid();
        var extension = Path.GetExtension(fileName);
        var key = $"{videoId}{extension}";
        var bucket = _storageOptions.VideoBucket;

        await _storageService.UploadFileAsync(fileStream, bucket, key, contentType, ct);

        var video = new Video
        {
            Id = videoId,
            Title = Path.GetFileNameWithoutExtension(fileName),
            StorageKey = $"{bucket}/{key}",
            OriginalFileName = fileName,
            FileSize = fileSize
        };

        var created = await _videoRepository.AddAsync(video, ct);
        return Result<VideoDto>.Success(created.ToDto());
    }

    public async Task<Result<string>> GetVideoUrlAsync(Guid id, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, ct);
        if (video is null)
            return Result<string>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);

        var parts = video.StorageKey.Split('/', 2);
        var bucket = parts.Length > 1 ? parts[0] : _storageOptions.VideoBucket;
        var key = parts.Length > 1 ? parts[1] : video.StorageKey;

        var url = await _storageService.GetPresignedUrlAsync(bucket, key, 3600, ct);
        return Result<string>.Success(url);
    }
}
