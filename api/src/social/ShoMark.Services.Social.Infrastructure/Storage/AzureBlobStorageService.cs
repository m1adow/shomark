using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Storage;

public class AzureBlobStorageService : IStorageService
{
    private readonly AzureBlobOptions _options;

    public AzureBlobStorageService(IOptions<AzureBlobOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadFileAsync(Stream stream, string bucket, string key, string contentType, CancellationToken ct = default)
    {
        var containerClient = new BlobContainerClient(_options.ConnectionString, bucket);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(key);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        return $"{bucket}/{key}";
    }

    public Task<string> GetPresignedUrlAsync(string bucket, string key, int expirySeconds = 3600, CancellationToken ct = default)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = bucket,
            BlobName = key,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddSeconds(expirySeconds)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var credential = new Azure.Storage.StorageSharedKeyCredential(_options.AccountName, _options.AccountKey);
        var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();

        var blobUri = new UriBuilder
        {
            Scheme = "https",
            Host = $"{_options.AccountName}.blob.core.windows.net",
            Path = $"{bucket}/{key}",
            Query = sasToken
        };

        return Task.FromResult(blobUri.ToString());
    }
}
