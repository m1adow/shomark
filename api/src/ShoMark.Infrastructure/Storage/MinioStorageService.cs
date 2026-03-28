using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using ShoMark.Application.Common;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;

    public MinioStorageService(IOptions<MinioOptions> options)
    {
        var opts = options.Value;
        _client = new MinioClient()
            .WithEndpoint(opts.Endpoint)
            .WithCredentials(opts.AccessKey, opts.SecretKey)
            .WithSSL(opts.Secure)
            .Build();
    }

    public async Task<string> UploadFileAsync(Stream stream, string bucket, string key, string contentType, CancellationToken ct = default)
    {
        var bucketExists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucket), ct);

        if (!bucketExists)
        {
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucket), ct);
        }

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType), ct);

        return $"{bucket}/{key}";
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string key, int expirySeconds = 3600, CancellationToken ct = default)
    {
        return await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithExpiry(expirySeconds));
    }
}
