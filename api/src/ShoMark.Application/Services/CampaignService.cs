using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Campaigns;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IAiFragmentRepository _fragmentRepository;
    private readonly IVideoRepository _videoRepository;

    public CampaignService(
        ICampaignRepository campaignRepository,
        ICurrentUserAccessor currentUser,
        IAiFragmentRepository fragmentRepository,
        IVideoRepository videoRepository)
    {
        _campaignRepository = campaignRepository;
        _currentUser = currentUser;
        _fragmentRepository = fragmentRepository;
        _videoRepository = videoRepository;
    }

    public async Task<Result<CampaignDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
            return Result<CampaignDto>.Failure(Constants.Errors.Messages.CampaignNotFound, Constants.Errors.Codes.NotFound);

        return Result<CampaignDto>.Success(campaign.ToDto());
    }

    public async Task<Result<IReadOnlyList<CampaignDto>>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var campaigns = await _campaignRepository.GetByUserIdAsync(userId, ct);
        return Result<IReadOnlyList<CampaignDto>>.Success(
            campaigns.Select(c => c.ToDto()).ToList());
    }

    public async Task<Result<IReadOnlyList<CampaignDto>>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default)
    {
        var campaigns = await _campaignRepository.GetByVideoIdAsync(videoId, ct);
        return Result<IReadOnlyList<CampaignDto>>.Success(
            campaigns.Select(c => c.ToDto()).ToList());
    }

    public async Task<Result<CampaignDto>> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var existing = await _campaignRepository.GetByUserAndNameAsync(userId, request.Name, ct);
            if (existing is not null)
                return Result<CampaignDto>.Failure(Constants.Errors.Messages.DuplicateCampaign, Constants.Errors.Codes.Duplicate);
        }

        if (request.FragmentId.HasValue)
        {
            var fragment = await _fragmentRepository.GetByIdAsync(request.FragmentId.Value, ct);
            if (fragment is null)
                return Result<CampaignDto>.Failure(Constants.Errors.Messages.FragmentNotFound, Constants.Errors.Codes.NotFound);
        }

        if (request.VideoId.HasValue)
        {
            var video = await _videoRepository.GetByIdAsync(request.VideoId.Value, ct);
            if (video is null)
                return Result<CampaignDto>.Failure(Constants.Errors.Messages.VideoNotFound, Constants.Errors.Codes.NotFound);
        }

        var campaign = new Campaign
        {
            UserId = userId,
            FragmentId = request.FragmentId,
            VideoId = request.VideoId,
            Name = request.Name,
            TargetAudience = request.TargetAudience,
            Description = request.Description
        };

        var created = await _campaignRepository.AddAsync(campaign, ct);
        return Result<CampaignDto>.Success(created.ToDto());
    }

    public async Task<Result<CampaignDto>> UpdateAsync(Guid id, UpdateCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
            return Result<CampaignDto>.Failure(Constants.Errors.Messages.CampaignNotFound, Constants.Errors.Codes.NotFound);

        if (request.Name is not null)
        {
            if (request.Name != campaign.Name)
            {
                var existing = await _campaignRepository.GetByUserAndNameAsync(campaign.UserId, request.Name, ct);
                if (existing is not null)
                    return Result<CampaignDto>.Failure(Constants.Errors.Messages.DuplicateCampaign, Constants.Errors.Codes.Duplicate);
            }
            campaign.Name = request.Name;
        }
        if (request.Status.HasValue) campaign.Status = request.Status.Value;
        if (request.TargetAudience.HasValue) campaign.TargetAudience = request.TargetAudience.Value;
        if (request.Description is not null) campaign.Description = request.Description;
        if (request.VideoId.HasValue) campaign.VideoId = request.VideoId.Value;
        if (request.FragmentId.HasValue) campaign.FragmentId = request.FragmentId.Value;

        await _campaignRepository.UpdateAsync(campaign, ct);
        return Result<CampaignDto>.Success(campaign.ToDto());
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
            return Result<bool>.Failure(Constants.Errors.Messages.CampaignNotFound, Constants.Errors.Codes.NotFound);

        await _campaignRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> IsNameAvailableAsync(string name, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        var existing = await _campaignRepository.GetByUserAndNameAsync(userId, name, ct);
        return Result<bool>.Success(existing is null);
    }

}
