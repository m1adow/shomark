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

    public CampaignService(
        ICampaignRepository campaignRepository,
        IUserRepository userRepository,
        IAiFragmentRepository fragmentRepository)
    {
        _campaignRepository = campaignRepository;
        _userRepository = userRepository;
        _fragmentRepository = fragmentRepository;
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

    public async Task<Result<CampaignDto>> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<CampaignDto>.Failure("User not found", "NOT_FOUND");

        var fragment = await _fragmentRepository.GetByIdAsync(request.FragmentId, ct);
        if (fragment is null)
            return Result<CampaignDto>.Failure("Fragment not found", "NOT_FOUND");

        var existing = await _campaignRepository.GetByUserAndFragmentAsync(request.UserId, request.FragmentId, ct);
        if (existing is not null)
            return Result<CampaignDto>.Failure(
                "A campaign already exists for this user and fragment", "DUPLICATE");

        var campaign = new Campaign
        {
            UserId = request.UserId,
            FragmentId = request.FragmentId,
            Name = request.Name
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
        c.Id, c.UserId, c.FragmentId, c.Name, c.Status.ToString(), c.CreatedAt, c.UpdatedAt);
}
