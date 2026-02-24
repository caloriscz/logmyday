using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogMyDay.Api.Controllers;

[Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
[ApiController]
[Route("api/input-types")]
public class InputTypesController : ControllerBase
{
    private readonly IInputTypeService _inputTypeService;

    public InputTypesController(IInputTypeService inputTypeService)
    {
        _inputTypeService = inputTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InputTypeDto>>> GetInputTypes()
    {
        var inputTypes = await _inputTypeService.GetAllInputTypes();
        
        return Ok(inputTypes);
    }
}
