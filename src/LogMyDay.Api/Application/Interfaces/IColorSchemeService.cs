using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IColorSchemeService
{
    Task<IList<ColorSchemeResponse>> GetAll(Guid userId);
    Task<ColorSchemeResponse> GetById(int id, Guid userId);
    Task<int> Create(ColorSchemeRequest request, Guid userId);
    Task Update(int id, ColorSchemeRequest request, Guid userId);
    Task Delete(int id, Guid userId);
}
