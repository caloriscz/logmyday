using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[Authorize(AuthenticationSchemes = "lmd-cookie")]
[ApiController]
[Route("api/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(IActivityService calendarService, ILogger<ActivitiesController> logger)
    {
        _activityService = calendarService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var calendar = await _activityService.GetById(id);

        if (calendar == null)
        {
            return NotFound();
        }

        return Ok(calendar);
    }    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] string orderBy = "desc", [FromQuery] int? tagId = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] string? descriptionFilter = null)
    {
        var pagedResult = await _activityService.GetPaged(pageNumber, pageSize, orderBy, tagId, startDate, endDate, descriptionFilter);
        return Ok(pagedResult);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ActivityRequest calendarRequest)
    {
        var createdCalendar = await _activityService.Create(calendarRequest);

        return Ok(createdCalendar);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DateTime dateCreated, DateTime? dateFinished)
    {
        return Ok(await _activityService.Update(id, dateCreated, dateFinished));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _activityService.Delete(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("by-date")]
    public async Task<IActionResult> GetByDate([FromBody] ActivityRequest request)
    {
        return Ok(await _activityService.GetByDate(request));
    }    [HttpGet("paged-by-weeks")]
    public async Task<IActionResult> GetPagedByWeeks([FromQuery] int weekPageNumber = 1, [FromQuery] int weeksPerPage = 12, [FromQuery] string orderBy = "desc", [FromQuery] int? tagId = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] string? descriptionFilter = null)
    {
        var pagedResult = await _activityService.GetPagedByWeeks(weekPageNumber, weeksPerPage, orderBy, tagId, startDate, endDate, descriptionFilter);
        return Ok(pagedResult);
    }

    [HttpGet("paged-by-months")]
    public async Task<IActionResult> GetPagedByMonths([FromQuery] int monthPageNumber = 1, [FromQuery] int monthsPerPage = 12, [FromQuery] string orderBy = "desc", [FromQuery] int? tagId = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] string? descriptionFilter = null)
    {        var pagedResult = await _activityService.GetPagedByMonths(monthPageNumber, monthsPerPage, orderBy, tagId, startDate, endDate, descriptionFilter);
        return Ok(pagedResult);
    }    [HttpGet("by-year")]
    public async Task<IActionResult> GetByYear([FromQuery] int year, [FromQuery] int? tagId = null)
    {
        var activities = await _activityService.GetByYear(year, tagId);
        return Ok(activities);
    }

    [HttpGet("available-years")]
    public async Task<IActionResult> GetAvailableYears([FromQuery] int? tagId = null)
    {
        var years = await _activityService.GetAvailableYears(tagId);
        return Ok(years);
    }

    [HttpGet("check-duplicate")]
    public async Task<IActionResult> CheckDuplicate([FromQuery] int tagId, [FromQuery] DateTime dateStarted)
    {
        var hasDuplicate = await _activityService.HasActivityForTimeGranularity(tagId, dateStarted);
        return Ok(new DuplicateCheckResponse { HasDuplicate = hasDuplicate });
    }

    [HttpGet("required-daily-tags-unfilled")]
    public async Task<IActionResult> GetRequiredDailyTagsNotFilledForDate([FromQuery] string dateString)
    {
        _logger.LogInformation("GetRequiredDailyTagsNotFilledForDate called with dateString: {DateString}", dateString);
        
        if (!DateTime.TryParse(dateString, out var date))
        {
            _logger.LogWarning("Invalid date format received: {DateString}", dateString);
            return BadRequest("Invalid date format. Please use a valid date string like '2025-08-31'.");
        }
        
        _logger.LogInformation("Parsed date: {ParsedDate}", date);
        var unfilledTags = await _activityService.GetRequiredDailyTagsNotFilledForDate(date);
        _logger.LogInformation("Returning {Count} unfilled required tags", unfilledTags.Count);
        return Ok(unfilledTags);
    }
}