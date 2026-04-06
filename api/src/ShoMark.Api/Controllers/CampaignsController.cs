using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoMark.Application.DTOs.Campaigns;
using ShoMark.Application.Interfaces;

namespace ShoMark.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ICurrentUserAccessor _currentUser;

    public CampaignsController(ICampaignService campaignService, ICurrentUserAccessor currentUser)
    {
        _campaignService = campaignService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyCampaigns(CancellationToken ct)
    {
        var result = await _campaignService.GetByUserIdAsync(_currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpGet("video/{videoId:guid}")]
    public async Task<IActionResult> GetByVideoId(Guid videoId, CancellationToken ct)
    {
        var result = await _campaignService.GetByVideoIdAsync(videoId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var result = await _campaignService.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.Error, result.ErrorCode });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCampaignRequest request, CancellationToken ct)
    {
        var result = await _campaignService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error, result.ErrorCode });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { result.Error, result.ErrorCode });
    }
}
