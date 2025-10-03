using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[Route("api/[controller]")]
public class UnitsController : BaseApiController
{
    private readonly IUnitService _unitService;
    private readonly ILogger<UnitsController> _logger;

    public UnitsController(IUnitService unitService, ILogger<UnitsController> logger, IAuthService authService)
        : base(authService)
    {
        _unitService = unitService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnitResponse>>> GetAll()
    {
        var units = await _unitService.GetAllAsync();
        return Ok(units);
    }

    [HttpGet("quantities")]
    public async Task<ActionResult<IEnumerable<QuantityResponse>>> GetQuantities()
    {
        var quantities = await _unitService.GetQuantitiesAsync();
        return Ok(quantities);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UnitResponse>> GetById(int id)
    {
        var unit = await _unitService.GetByIdAsync(id);
        return Ok(unit);
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(UnitRequest request)
    {
        var id = await _unitService.CreateAsync(request);
        _logger.LogInformation("Created unit {UnitId}", id);
        return Ok(id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UnitRequest request)
    {
        await _unitService.UpdateAsync(id, request);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _unitService.DeleteAsync(id);
        return NoContent();
    }
}
