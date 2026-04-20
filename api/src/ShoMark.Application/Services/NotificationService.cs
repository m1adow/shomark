using System.Text.Json;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Notifications;
using ShoMark.Application.Interfaces;
using ShoMark.Application.Mappings;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Enums;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationSseNotifier _notifier;

    public NotificationService(
        INotificationRepository notificationRepository,
        INotificationSseNotifier notifier)
    {
        _notificationRepository = notificationRepository;
        _notifier = notifier;
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> GetByUserIdAsync(Guid userId, int take = 50, CancellationToken ct = default)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId, take, ct);
        return Result<IReadOnlyList<NotificationDto>>.Success(
            notifications.Select(n => n.ToDto()).ToList());
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await _notificationRepository.GetUnreadCountAsync(userId, ct);
        return Result<int>.Success(count);
    }

    public async Task<Result<NotificationDto>> CreateAsync(
        Guid userId, NotificationType type, string title,
        string? message, Guid? referenceId, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
        };

        var created = await _notificationRepository.AddAsync(notification, ct);
        var dto = created.ToDto();

        var ssePayload = JsonSerializer.Serialize(dto);
        await _notifier.PublishAsync(userId, ssePayload);

        return Result<NotificationDto>.Success(dto);
    }

    public async Task<Result<bool>> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(id, ct);
        if (notification is null)
            return Result<bool>.Failure(Constants.Errors.Messages.NotificationNotFound, Constants.Errors.Codes.NotFound);

        await _notificationRepository.MarkAsReadAsync(id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _notificationRepository.MarkAllAsReadAsync(userId, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(id, ct);
        if (notification is null)
            return Result<bool>.Failure(Constants.Errors.Messages.NotificationNotFound, Constants.Errors.Codes.NotFound);

        await _notificationRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

}
