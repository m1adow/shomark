namespace ShoMark.Application.DTOs.Publishing;

public record PublishRequest(
    string AccessToken,
    string? Title,
    string? Content,
    string? MediaUrl,
    string? MediaContentType);

public record PublishResult(
    bool Success,
    string? ExternalUrl,
    string? ExternalPostId,
    string? ErrorMessage);
