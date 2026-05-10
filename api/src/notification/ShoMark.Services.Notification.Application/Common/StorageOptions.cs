namespace ShoMark.Application.Common;

public enum StorageProvider
{
    Minio,
    AzureBlob
}

public class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.Minio;
    public string VideoBucket { get; set; } = "videos";
}
