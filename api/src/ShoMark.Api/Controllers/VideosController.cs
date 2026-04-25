using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Videos;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VideosController : ControllerBase
{
    private readonly IVideoService _videoService;
    private readonly VideoOptions _videoOptions;

    public VideosController(IVideoService videoService, IOptions<VideoOptions> videoOptions)
    {
        _videoService = videoService;
        _videoOptions = videoOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _videoService.GetAllAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _videoService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpGet("{id:guid}/fragments")]
    public async Task<IActionResult> GetWithFragments(Guid id, CancellationToken ct)
    {
        var result = await _videoService.GetWithFragmentsAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVideoRequest request, CancellationToken ct)
    {
        var result = await _videoService.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVideoRequest request, CancellationToken ct)
    {
        var result = await _videoService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _videoService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }

    /// <summary>
    /// Trigger AI highlight processing for a video.
    /// Sends a Kafka message to the worker which will transcribe, find highlights,
    /// cut clips, and upload them to MinIO. Results are consumed asynchronously
    /// and persisted as AiFragment records.
    /// </summary>
    [HttpPost("{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id, [FromBody] ProcessVideoRequest request, CancellationToken ct)
    {
        var result = await _videoService.ProcessVideoAsync(id, request, ct);
        return result.IsSuccess
            ? Accepted(new { message = "Video processing started" })
            : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { Error = Constants.Errors.Messages.NoFileProvided, ErrorCode = Constants.Errors.Codes.Validation });

        if (!_videoOptions.AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { Error = Constants.Errors.Messages.InvalidFileType, ErrorCode = Constants.Errors.Codes.Validation });

        await using var stream = file.OpenReadStream();
        var result = await _videoService.UploadAsync(stream, file.FileName, file.Length, file.ContentType, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpGet("{id:guid}/url")]
    public async Task<IActionResult> GetVideoUrl(Guid id, CancellationToken ct)
    {
        var result = await _videoService.GetVideoUrlAsync(id, ct);
        return result.IsSuccess ? Ok(new { url = result.Value }) : NotFound(new { result.Error, result.ErrorCode });
    }
}
