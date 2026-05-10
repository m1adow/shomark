namespace ShoMark.Application.Interfaces;

public interface IPostPublishingProducer
{
    Task ProduceAsync(Guid postId, CancellationToken ct = default);
}
