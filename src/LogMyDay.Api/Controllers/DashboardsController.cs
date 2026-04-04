using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardsController : BaseApiController
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardsController> _logger;

    public DashboardsController(
        IDashboardService dashboardService,
        IAuthService authService,
        ILogger<DashboardsController> logger) : base(authService)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<DashboardResponse>>> GetDashboards()
    {
        var userId = GetCurrentUserId();
        var dashboards = await _dashboardService.GetDashboards(userId);

        return Ok(dashboards);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var dashboard = await _dashboardService.GetDashboard(id, userId);

            return Ok(dashboard);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dashboard {DashboardId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpGet("default")]
    public async Task<ActionResult<DashboardResponse>> GetOrCreateDefault()
    {
        var userId = GetCurrentUserId();
        var dashboard = await _dashboardService.GetOrCreateDefault(userId);

        return Ok(dashboard);
    }

    [HttpPost]
    public async Task<ActionResult<DashboardResponse>> Create(DashboardRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var created = await _dashboardService.Create(request, userId);

            return CreatedAtAction(nameof(GetDashboard), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid dashboard create request");

            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DashboardRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _dashboardService.Update(id, request, userId);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid dashboard update request for {DashboardId}", id);

            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dashboard {DashboardId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _dashboardService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dashboard {DashboardId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpGet("{dashboardId:int}/panels")]
    public async Task<ActionResult<IList<DashboardPanelResponse>>> GetPanels(int dashboardId)
    {
        var userId = GetCurrentUserId();
        try
        {
            var panels = await _dashboardService.GetPanels(dashboardId, userId);

            return Ok(panels);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dashboard {DashboardId} not found for user {UserId}", dashboardId, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPost("{dashboardId:int}/panels")]
    public async Task<ActionResult<DashboardPanelResponse>> AddPanel(int dashboardId, DashboardPanelRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var panel = await _dashboardService.AddPanel(dashboardId, request, userId);

            return Created($"/api/dashboards/{dashboardId}/panels/{panel.Id}", panel);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid panel request for dashboard {DashboardId}", dashboardId);

            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found for panel operation on dashboard {DashboardId}", dashboardId);

            return NotFound(ex.Message);
        }
    }

    [HttpPut("{dashboardId:int}/panels/{panelId:int}")]
    public async Task<IActionResult> UpdatePanel(int dashboardId, int panelId, DashboardPanelRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _dashboardService.UpdatePanel(dashboardId, panelId, request, userId);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid panel update for panel {PanelId} on dashboard {DashboardId}", panelId, dashboardId);

            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Panel {PanelId} not found on dashboard {DashboardId}", panelId, dashboardId);

            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{dashboardId:int}/panels/{panelId:int}")]
    public async Task<IActionResult> RemovePanel(int dashboardId, int panelId)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _dashboardService.RemovePanel(dashboardId, panelId, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Panel {PanelId} not found on dashboard {DashboardId}", panelId, dashboardId);

            return NotFound(ex.Message);
        }
    }

    [HttpPut("{dashboardId:int}/panels/reorder")]
    public async Task<IActionResult> ReorderPanels(int dashboardId, List<PanelReorderRequest> request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _dashboardService.ReorderPanels(dashboardId, request, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dashboard {DashboardId} not found for reorder", dashboardId);

            return NotFound(ex.Message);
        }
    }

    [HttpGet("{dashboardId:int}/data")]
    public async Task<ActionResult<DashboardDataResponse>> GetDashboardData(int dashboardId)
    {
        var userId = GetCurrentUserId();
        try
        {
            var data = await _dashboardService.GetDashboardData(dashboardId, userId);

            return Ok(data);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dashboard {DashboardId} not found for user {UserId}", dashboardId, userId);

            return NotFound(ex.Message);
        }
    }
}
