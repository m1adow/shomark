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

    public AiFragmentService(IAiFragmentRepository fragmentRepository, IVideoRepository videoRepository)
    {
        _fragmentRepository = fragmentRepository;
        _videoRepository = videoRepository;
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
            fragment.StartTime, fragment.EndTime, fragment.MinioKey,
            fragment.ViralScore, fragment.Hashtags, fragment.ThumbnailKey,
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

        if (request.StartTime >= request.EndTime)
            return Result<AiFragmentDto>.Failure("StartTime must be less than EndTime", "VALIDATION_ERROR");

        fragment.Description = request.Description;
        fragment.StartTime = request.StartTime;
        fragment.EndTime = request.EndTime;
        fragment.MinioKey = request.MinioKey;
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

    private static AiFragmentDto MapToDto(AiFragment f) => new(
        f.Id, f.VideoId, f.Description, f.StartTime, f.EndTime, f.MinioKey,
        f.ViralScore, f.Hashtags, f.ThumbnailKey, f.IsApproved, f.CreatedAt, f.UpdatedAt);
}
