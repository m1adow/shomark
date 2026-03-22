using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Tags;

namespace ShoMark.Application.Interfaces;

public interface ITagService
{
    Task<Result<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TagDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<TagDto>> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<Result<TagDto>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
