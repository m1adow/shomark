namespace ShoMark.Application.DTOs.Notifications;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string? Message,
    Guid? ReferenceId,
    bool IsRead,
    DateTime CreatedAt);
