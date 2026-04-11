using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly INotificationSseNotifier _notifier;
    private readonly ICurrentUserAccessor _currentUser;

    public NotificationsController(
        INotificationService notificationService,
        INotificationSseNotifier notifier,
        ICurrentUserAccessor currentUser)
    {
        _notificationService = notificationService;
        _notifier = notifier;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var result = await _notificationService.GetByUserIdAsync(_currentUser.UserId, take, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await _notificationService.GetUnreadCountAsync(_currentUser.UserId, ct);
        return result.IsSuccess ? Ok(new { count = result.Value }) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkAsReadAsync(id, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllAsReadAsync(_currentUser.UserId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }

    /// <summary>
    /// SSE endpoint — streams notification events for the authenticated user.
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var userId = _currentUser.UserId;
        var reader = _notifier.Subscribe(userId);

        try
        {
            await foreach (var payload in reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync($"event: notification\ndata: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected — normal
        }
        finally
        {
            _notifier.Unsubscribe(userId, reader);
        }
    }
}
