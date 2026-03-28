using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Campaigns;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAiFragmentRepository _fragmentRepository;
    private readonly IVideoRepository _videoRepository;

    public CampaignService(
        ICampaignRepository campaignRepository,
        IUserRepository userRepository,
        IAiFragmentRepository fragmentRepository,
        IVideoRepository videoRepository)
    {
        _campaignRepository = campaignRepository;
        _userRepository = userRepository;
        _fragmentRepository = fragmentRepository;
        _videoRepository = videoRepository;
    }

    public async Task<Result<CampaignDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
            return Result<CampaignDto>.Failure("Campaign not found", "NOT_FOUND");

        return Result<CampaignDto>.Success(MapToDto(campaign));
    }

    public async Task<Result<IReadOnlyList<CampaignDto>>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var campaigns = await _campaignRepository.GetByUserIdAsync(userId, ct);
        return Result<IReadOnlyList<CampaignDto>>.Success(
            campaigns.Select(MapToDto).ToList());
    }

    public async Task<Result<IReadOnlyList<CampaignDto>>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default)
    {
        var campaigns = await _campaignRepository.GetByVideoIdAsync(videoId, ct);
        return Result<IReadOnlyList<CampaignDto>>.Success(
            campaigns.Select(MapToDto).ToList());
    }

    public async Task<Result<CampaignDto>> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<CampaignDto>.Failure("User not found", "NOT_FOUND");

        if (request.FragmentId.HasValue)
        {
            var fragment = await _fragmentRepository.GetByIdAsync(request.FragmentId.Value, ct);
            if (fragment is null)
                return Result<CampaignDto>.Failure("Fragment not found", "NOT_FOUND");
        }

        if (request.VideoId.HasValue)
        {
            var video = await _videoRepository.GetByIdAsync(request.VideoId.Value, ct);
            if (video is null)
                return Result<CampaignDto>.Failure("Video not found", "NOT_FOUND");
        }

        var campaign = new Campaign
        {
            UserId = request.UserId,
            FragmentId = request.FragmentId,
            VideoId = request.VideoId,
            Name = request.Name,
            TargetAudience = request.TargetAudience,
            Description = request.Description
        };

        var created = await _campaignRepository.AddAsync(campaign, ct);
        return Result<CampaignDto>.Success(MapToDto(created));
    }

    public async Task<Result<CampaignDto>> UpdateAsync(Guid id, UpdateCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
            return Result<CampaignDto>.Failure("Campaign not found", "NOT_FOUND");

        if (request.Name is not null) campaign.Name = request.Name;
        if (request.Status.HasValue) campaign.Status = request.Status.Value;
        if (request.TargetAudience.HasValue) campaign.TargetAudience = request.TargetAudience.Value;
        if (request.Description is not null) campaign.Description = request.Description;
        if (request.VideoId.HasValue) campaign.VideoId = request.VideoId.Value;
        if (request.FragmentId.HasValue) campaign.FragmentId = request.FragmentId.Value;

        await _campaignRepository.UpdateAsync(campaign, ct);
        return Result<CampaignDto>.Success(MapToDto(campaign));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
            return Result<bool>.Failure("Campaign not found", "NOT_FOUND");

        await _campaignRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    private static CampaignDto MapToDto(Campaign c) => new(
        c.Id, c.UserId, c.FragmentId, c.VideoId, c.Name,
        c.TargetAudience?.ToString(), c.Description,
        c.Status.ToString(), c.CreatedAt, c.UpdatedAt);
}
