using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class InputTypeService : IInputTypeService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<InputTypeService> _logger;

    public InputTypeService(LogMyDayDbContext context, ILogger<InputTypeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<InputTypeDto>> GetAllInputTypes()
    {
        _logger.LogInformation("Retrieving all input types");

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

        _logger.LogInformation("Retrieved {Count} input types", inputTypes.Count);

        return inputTypes;
    }
}
