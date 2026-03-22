using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoMark.Application.DTOs.Fragments;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FragmentsController : ControllerBase
{
    private readonly IAiFragmentService _fragmentService;

    public FragmentsController(IAiFragmentService fragmentService)
    {
        _fragmentService = fragmentService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _fragmentService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpGet("video/{videoId:guid}")]
    public async Task<IActionResult> GetByVideoId(Guid videoId, CancellationToken ct)
    {
        var result = await _fragmentService.GetByVideoIdAsync(videoId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> GetWithDetails(Guid id, CancellationToken ct)
    {
        var result = await _fragmentService.GetWithDetailsAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAiFragmentRequest request, CancellationToken ct)
    {
        var result = await _fragmentService.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAiFragmentRequest request, CancellationToken ct)
    {
        var result = await _fragmentService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _fragmentService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPost("{fragmentId:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> AddTag(Guid fragmentId, Guid tagId, CancellationToken ct)
    {
        var result = await _fragmentService.AddTagAsync(fragmentId, tagId, ct);
        return result.IsSuccess ? Ok() : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpDelete("{fragmentId:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> RemoveTag(Guid fragmentId, Guid tagId, CancellationToken ct)
    {
        var result = await _fragmentService.RemoveTagAsync(fragmentId, tagId, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }
}
