using ShoMark.Domain.Entities;

namespace ShoMark.Domain.Interfaces;

public interface IVideoRepository : IRepository<Video>
{
    Task<Video?> GetByMinioKeyAsync(string minioKey, CancellationToken ct = default);
    Task<IReadOnlyList<Video>> GetActiveVideosAsync(CancellationToken ct = default);
    Task<Video?> GetWithFragmentsAsync(Guid id, CancellationToken ct = default);
}
