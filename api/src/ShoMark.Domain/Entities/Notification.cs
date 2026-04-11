using ShoMark.Domain.Enums;

namespace ShoMark.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
}
