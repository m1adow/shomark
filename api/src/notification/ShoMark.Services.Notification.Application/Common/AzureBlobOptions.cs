namespace ShoMark.Application.Common;

public class AzureBlobOptions
{
    public const string SectionName = "AzureBlob";

    public string ConnectionString { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountKey { get; set; } = string.Empty;
}
