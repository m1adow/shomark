using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Notifications;
using ShoMark.Domain.Enums;

namespace ShoMark.Application.Interfaces;

public interface INotificationService
{
    Task<Result<IReadOnlyList<NotificationDto>>> GetByUserIdAsync(Guid userId, int take = 50, CancellationToken ct = default);
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<Result<NotificationDto>> CreateAsync(Guid userId, NotificationType type, string title, string? message, Guid? referenceId, CancellationToken ct = default);
    Task<Result<bool>> MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
