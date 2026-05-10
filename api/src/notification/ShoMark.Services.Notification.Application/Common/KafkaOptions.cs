namespace ShoMark.Application.Common;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "kafka:29092";
    public string VideoProcessingTopic { get; set; } = "video-processing";
    public string CompletionTopic { get; set; } = "video-processing-completed";
    public string ConsumerGroupId { get; set; } = "api-group";
}
