namespace ShoMark.Application.Interfaces;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream stream, string bucket, string key, string contentType, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string key, int expirySeconds = 3600, CancellationToken ct = default);
}
