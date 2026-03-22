using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Fragments;

namespace ShoMark.Application.Interfaces;

public interface IAiFragmentService
{
    Task<Result<AiFragmentDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AiFragmentDto>>> GetByVideoIdAsync(Guid videoId, CancellationToken ct = default);
    Task<Result<AiFragmentDetailDto>> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<Result<AiFragmentDto>> CreateAsync(CreateAiFragmentRequest request, CancellationToken ct = default);
    Task<Result<AiFragmentDto>> UpdateAsync(Guid id, UpdateAiFragmentRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> AddTagAsync(Guid fragmentId, Guid tagId, CancellationToken ct = default);
    Task<Result<bool>> RemoveTagAsync(Guid fragmentId, Guid tagId, CancellationToken ct = default);
}
