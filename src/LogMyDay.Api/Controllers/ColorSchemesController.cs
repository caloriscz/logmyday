using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/color-schemes")]
public class ColorSchemesController : BaseApiController
{
    private readonly IColorSchemeService _colorSchemeService;
    private readonly ILogger<ColorSchemesController> _logger;

    public ColorSchemesController(
        IColorSchemeService colorSchemeService,
        IAuthService authService,
        ILogger<ColorSchemesController> logger) : base(authService)
    {
        _colorSchemeService = colorSchemeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<ColorSchemeResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();
        var schemes = await _colorSchemeService.GetAll(userId);

        return Ok(schemes);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ColorSchemeResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var scheme = await _colorSchemeService.GetById(id, userId);

            return Ok(scheme);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Color scheme {SchemeId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(ColorSchemeRequest request)
    {
        var userId = GetCurrentUserId();
        var id = await _colorSchemeService.Create(request, userId);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ColorSchemeRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _colorSchemeService.Update(id, request, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Color scheme {SchemeId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _colorSchemeService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Color scheme {SchemeId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }
}
