namespace ShoMark.Application.Common;

public class VideoOptions
{
    public const string SectionName = "Video";

    public string[] AllowedContentTypes { get; set; } = ["video/mp4", "video/quicktime"];
    public long MaxFileSizeBytes { get; set; } = 2L * 1024 * 1024 * 1024;
}
