namespace ShoMark.Contracts;

public static class KafkaTopics
{
    public const string VideoProcessing = "video-processing";
    public const string VideoProcessingCompleted = "video-processing-completed";
    public const string VideoProcessingSucceeded = "video-processing-succeeded";
    public const string FragmentApproved = "fragment-approved";
    public const string CampaignStatusChanged = "campaign-status-changed";
    public const string PostPublished = "post-published";
    public const string PostFailed = "post-failed";
}
