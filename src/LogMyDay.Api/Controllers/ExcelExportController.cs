using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[Route("api/[controller]")]
public class ExcelExportController : BaseApiController
{
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<ExcelExportController> _logger;

    public ExcelExportController(
        IExcelExportService excelExportService, 
        ILogger<ExcelExportController> logger,
        IAuthService authService) : base(authService)
    {
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate and download Excel report for selected tags
    /// </summary>
    /// <param name="request">Export configuration</param>
    /// <returns>Excel file download</returns>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateExcelReport([FromBody] ExcelExportRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            request.UserId = userId;
            
            _logger.LogInformation("Excel export request received for {TagCount} tags, User: {UserId}", request.TagIds.Count, userId);

            var result = await _excelExportService.GenerateExcelReport(request);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            if (result.FileContent == null)
            {
                return StatusCode(500, new { message = "Failed to generate file content" });
            }

            return File(
                result.FileContent,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.FileName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Excel export");

            return StatusCode(500, new { message = "Excel export failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Get available tags for Excel export (current user only)
    /// </summary>
    /// <returns>List of available tags for the authenticated user</returns>
    [HttpGet("tags")]
    public async Task<IActionResult> GetAvailableTags()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Get available tags request for user: {UserId}", userId);

            var tags = await _excelExportService.GetAvailableTags(userId);

            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tags");

            return StatusCode(500, new { message = "Failed to get available tags", error = ex.Message });
        }
    }

    /// <summary>
    /// Get preview statistics for Excel export
    /// </summary>
    /// <param name="request">Export configuration</param>
    /// <returns>Preview statistics</returns>
    [HttpPost("preview")]
    public async Task<IActionResult> GetExportPreview([FromBody] ExcelExportRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            request.UserId = userId;
            
            _logger.LogInformation("Export preview request for {TagCount} tags, User: {UserId}", request.TagIds.Count, userId);

            var statistics = await _excelExportService.GetExportPreview(request);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export preview");

            return StatusCode(500, new { message = "Failed to get export preview", error = ex.Message });
        }
    }
}
