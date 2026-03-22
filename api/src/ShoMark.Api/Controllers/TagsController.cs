using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoMark.Application.DTOs.Tags;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _tagService.GetAllAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _tagService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken ct)
    {
        var result = await _tagService.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTagRequest request, CancellationToken ct)
    {
        var result = await _tagService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _tagService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }
}
