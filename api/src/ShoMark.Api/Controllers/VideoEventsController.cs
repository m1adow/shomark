using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/videos")]
public class VideoEventsController : ControllerBase
{
    private readonly IVideoProcessingNotifier _notifier;

    public VideoEventsController(IVideoProcessingNotifier notifier)
    {
        _notifier = notifier;
    }

    /// <summary>
    /// SSE endpoint — streams processing events for a specific video.
    /// Client connects to GET /api/videos/{videoId}/events and receives
    /// "processing-complete" events when highlights are ready.
    /// </summary>
    [HttpGet("{videoId:guid}/events")]
    public async Task StreamEvents(Guid videoId, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var reader = _notifier.Subscribe(videoId);

        try
        {
            await foreach (var payload in reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync($"event: processing-complete\ndata: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected — normal
        }
        finally
        {
            _notifier.Unsubscribe(videoId, reader);
        }
    }
}
