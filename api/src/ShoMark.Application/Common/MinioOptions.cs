namespace ShoMark.Application.Common;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "admin";
    public string SecretKey { get; set; } = "password123";
    public bool Secure { get; set; }
    public string VideoBucket { get; set; } = "videos";
}
