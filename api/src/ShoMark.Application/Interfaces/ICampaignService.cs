using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Campaigns;

namespace ShoMark.Application.Interfaces;

public interface ICampaignService
{
    Task<Result<CampaignDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CampaignDto>>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<CampaignDto>> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default);
    Task<Result<CampaignDto>> UpdateAsync(Guid id, UpdateCampaignRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
