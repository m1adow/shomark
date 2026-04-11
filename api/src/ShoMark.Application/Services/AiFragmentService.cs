using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Fragments;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class AiFragmentService : IAiFragmentService
{
    private readonly IAiFragmentRepository _fragmentRepository;
    private readonly IVideoRepository _videoRepository;
    private readonly IStorageService _storageService;

    public AiFragmentService(
        IAiFragmentRepository fragmentRepository,
        IVideoRepository videoRepository,
        IStorageService storageService)
    {
        _fragmentRepository = fragmentRepository;
        _videoRepository = videoRepository;
        _storageService = storageService;
    }

    public async Task<Result<AiFragmentDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var fragment = await _fragmentRepository.GetByIdAsync(id, ct);
        if (fragment is null)
            return Result<AiFragmentDto>.Failure("Fragment not found", "NOT_FOUND");

        return Result<AiFragmentDto>.Success(MapToDto(fragment));
    }

    public async Task<Result<IReadOnlyList<AiFragmentDto>>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default)
    {
        var fragments = await _fragmentRepository.GetByVideoIdAsync(videoId, ct);
        return Result<IReadOnlyList<AiFragmentDto>>.Success(
            fragments.Select(MapToDto).ToList());
    }

    public async Task<Result<AiFragmentDetailDto>> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        var fragment = await _fragmentRepository.GetWithDetailsAsync(id, ct);
        if (fragment is null)
            return Result<AiFragmentDetailDto>.Failure("Fragment not found", "NOT_FOUND");

        var dto = new AiFragmentDetailDto(
            fragment.Id, fragment.VideoId, fragment.Description,
            fragment.StartTime, fragment.EndTime,
            fragment.MinioKey, CalculateViralScore(fragment),
            fragment.Hashtags, fragment.ThumbnailKey,
            fragment.IsApproved, fragment.CreatedAt,
            fragment.Posts.Select(p => new PostSummaryDto(p.Id, p.Title, p.Status.ToString())).ToList());

        return Result<AiFragmentDetailDto>.Success(dto);
    }

    public async Task<Result<AiFragmentDto>> CreateAsync(CreateAiFragmentRequest request, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, ct);
        if (video is null)
            return Result<AiFragmentDto>.Failure("Video not found", "NOT_FOUND");

        if (request.StartTime >= request.EndTime)
            return Result<AiFragmentDto>.Failure("StartTime must be less than EndTime", "VALIDATION_ERROR");

        var fragment = new AiFragment
        {
            VideoId = request.VideoId,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            MinioKey = request.MinioKey,
            ViralScore = request.ViralScore,
            Hashtags = request.Hashtags,
            ThumbnailKey = request.ThumbnailKey
        };

        var created = await _fragmentRepository.AddAsync(fragment, ct);
        return Result<AiFragmentDto>.Success(MapToDto(created));
    }

    public async Task<Result<AiFragmentDto>> UpdateAsync(Guid id, UpdateAiFragmentRequest request, CancellationToken ct = default)
    {
        var fragment = await _fragmentRepository.GetByIdAsync(id, ct);
        if (fragment is null)
            return Result<AiFragmentDto>.Failure("Fragment not found", "NOT_FOUND");

        if (request.StartTime.HasValue || request.EndTime.HasValue)
        {
            var start = request.StartTime ?? fragment.StartTime;
            var end = request.EndTime ?? fragment.EndTime;
            if (start >= end)
                return Result<AiFragmentDto>.Failure("StartTime must be less than EndTime", "VALIDATION_ERROR");
        }

        if (request.Description is not null) fragment.Description = request.Description;
        if (request.StartTime.HasValue) fragment.StartTime = request.StartTime.Value;
        if (request.EndTime.HasValue) fragment.EndTime = request.EndTime.Value;
        if (request.MinioKey is not null) fragment.MinioKey = request.MinioKey;
        if (request.ViralScore.HasValue) fragment.ViralScore = request.ViralScore.Value;
        if (request.Hashtags is not null) fragment.Hashtags = request.Hashtags;
        if (request.IsApproved.HasValue) fragment.IsApproved = request.IsApproved.Value;

        await _fragmentRepository.UpdateAsync(fragment, ct);
        return Result<AiFragmentDto>.Success(MapToDto(fragment));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var fragment = await _fragmentRepository.GetByIdAsync(id, ct);
        if (fragment is null)
            return Result<bool>.Failure("Fragment not found", "NOT_FOUND");

        await _fragmentRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<string>> GetThumbnailUrlAsync(Guid id, CancellationToken ct = default)
    {
        var fragment = await _fragmentRepository.GetByIdAsync(id, ct);
        if (fragment is null)
            return Result<string>.Failure("Fragment not found", "NOT_FOUND");

        if (string.IsNullOrEmpty(fragment.ThumbnailKey))
            return Result<string>.Failure("No thumbnail available", "NOT_FOUND");

        var url = await GetPresignedUrlAsync(fragment.ThumbnailKey, ct);
        if (url is null)
            return Result<string>.Failure("Failed to generate thumbnail URL", "STORAGE_ERROR");

        return Result<string>.Success(url);
    }

    private static AiFragmentDto MapToDto(AiFragment f) => new(
        f.Id, f.VideoId, f.Description,
        f.StartTime, f.EndTime,
        f.MinioKey, CalculateViralScore(f), f.Hashtags,
        f.ThumbnailKey, f.IsApproved, f.CreatedAt, f.UpdatedAt);

    private async Task<string?> GetPresignedUrlAsync(string? storageKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(storageKey)) return null;
        try
        {
            var parts = storageKey.Split('/', 2);
            var bucket = parts.Length > 1 ? parts[0] : "highlights";
            var key = parts.Length > 1 ? parts[1] : storageKey;
            return await _storageService.GetPresignedUrlAsync(bucket, key, 3600, ct);
        }
        catch
        {
            return null;
        }
    }

    private static double CalculateViralScore(AiFragment f)
    {
        double baseScore = f.ViralScore.HasValue ? f.ViralScore.Value * 10.0 : 5.0;

        double duration = f.EndTime - f.StartTime;
        double durationFactor = duration switch
        {
            < 10 => 0.7,
            < 15 => 0.85,
            < 30 => 1.0,
            < 60 => 0.95,
            < 120 => 0.8,
            _ => 0.6
        };

        double contentBonus = 0;
        if (!string.IsNullOrWhiteSpace(f.Description)) contentBonus += 0.5;
        if (!string.IsNullOrWhiteSpace(f.Hashtags))
        {
            var tagCount = f.Hashtags.Split([' ', ',', '#'], StringSplitOptions.RemoveEmptyEntries).Length;
            contentBonus += Math.Min(tagCount * 0.2, 1.0);
        }

        double score = (baseScore * durationFactor) + contentBonus;
        return Math.Round(Math.Clamp(score, 0, 10), 1);
    }
}
