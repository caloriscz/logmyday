using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IColorSchemeApi
{
    [Get("/api/color-schemes")]
    Task<IList<ColorSchemeResponse>> GetColorSchemes();

    [Get("/api/color-schemes/{id}")]
    Task<ColorSchemeResponse> GetColorSchemeById(int id);

    [Post("/api/color-schemes")]
    Task<int> CreateColorScheme([Body] ColorSchemeRequest request);

    [Put("/api/color-schemes/{id}")]
    Task UpdateColorScheme(int id, [Body] ColorSchemeRequest request);

    [Delete("/api/color-schemes/{id}")]
    Task DeleteColorScheme(int id);
}
