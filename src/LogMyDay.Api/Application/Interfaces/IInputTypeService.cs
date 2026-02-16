using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IInputTypeService
{
    Task<IEnumerable<InputTypeDto>> GetAllInputTypes();
}
