using ShoMark.Application.DTOs.Notifications;
using ShoMark.Domain.Entities;

namespace ShoMark.Application.Mappings;

public static class DtoMappingExtensions
{
    public static NotificationDto ToDto(this Notification n) => new(
        n.Id, n.UserId, n.Type.ToString(), n.Title, n.Message,
        n.ReferenceId, n.IsRead, n.CreatedAt);
}
