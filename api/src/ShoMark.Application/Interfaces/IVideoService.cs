using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Videos;

namespace ShoMark.Application.Interfaces;

public interface IVideoService
{
    Task<Result<VideoDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<VideoDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<VideoWithFragmentsDto>> GetWithFragmentsAsync(Guid id, CancellationToken ct = default);
    Task<Result<VideoDto>> CreateAsync(CreateVideoRequest request, CancellationToken ct = default);
    Task<Result<VideoDto>> UploadAsync(Stream fileStream, string fileName, long fileSize, string contentType, CancellationToken ct = default);
    Task<Result<VideoDto>> UpdateAsync(Guid id, UpdateVideoRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> ProcessVideoAsync(Guid id, ProcessVideoRequest request, CancellationToken ct = default);
    Task<Result<string>> GetVideoUrlAsync(Guid id, CancellationToken ct = default);
}
