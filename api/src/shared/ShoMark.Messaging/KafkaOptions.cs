using ShoMark.Contracts;

namespace ShoMark.Messaging;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "kafka:29092";
    public string VideoProcessingTopic { get; set; } = KafkaTopics.VideoProcessing;
    public string CompletionTopic { get; set; } = KafkaTopics.VideoProcessingCompleted;
    public string VideoProcessingSucceededTopic { get; set; } = KafkaTopics.VideoProcessingSucceeded;
    public string FragmentApprovedTopic { get; set; } = KafkaTopics.FragmentApproved;
    public string CampaignStatusChangedTopic { get; set; } = KafkaTopics.CampaignStatusChanged;
    public string PostPublishedTopic { get; set; } = KafkaTopics.PostPublished;
    public string PostFailedTopic { get; set; } = KafkaTopics.PostFailed;
    public string ConsumerGroupId { get; set; } = "api-group";
}
