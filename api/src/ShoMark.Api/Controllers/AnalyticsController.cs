using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoMark.Application.DTOs.Analytics;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("post/{postId:guid}")]
    public async Task<IActionResult> GetByPostId(Guid postId, CancellationToken ct)
    {
        var result = await _analyticsService.GetByPostIdAsync(postId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpPut("post/{postId:guid}")]
    public async Task<IActionResult> Upsert(Guid postId, [FromBody] UpdateAnalyticsRequest request, CancellationToken ct)
    {
        var result = await _analyticsService.UpsertAsync(postId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }
}
