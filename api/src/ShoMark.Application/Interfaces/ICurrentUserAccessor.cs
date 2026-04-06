namespace ShoMark.Application.Interfaces;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
    string Email { get; }
    string Name { get; }
    bool IsAuthenticated { get; }
}
