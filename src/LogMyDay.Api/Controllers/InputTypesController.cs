using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogMyDay.Api.Infrastructure.Data;

namespace LogMyDay.Api.Controllers;

[Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
[ApiController]
[Route("api/input-types")]
public class InputTypesController : ControllerBase
{
    private readonly LogMyDayDbContext _context;

    public InputTypesController(LogMyDayDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InputTypeDto>>> GetInputTypes()
    {
        var inputTypes = await _context.InputTypes
            .Select(x => new InputTypeDto 
            { 
                Id = x.Id, 
                Name = x.Name,
                Description = x.Description,
                IsRangeEditable = x.IsRangeEditable,
                IsMinimumEditable = x.IsMinimumEditable,
                IsMaximumEditable = x.IsMaximumEditable,
                IsStepEditable = x.IsStepEditable,
                IsRepeatableEditable = x.IsRepeatableEditable
            })
            .ToListAsync();
        
        return Ok(inputTypes);
    }
}
