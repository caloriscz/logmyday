using LogMyDay.Shared.DTOs.Settings;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ISettingsApi
{
    [Get("/api/settings/ai")]
    Task<AiSettingsDto> GetAiSettings(CancellationToken cancellationToken = default);

    [Put("/api/settings/ai")]
    Task UpdateAiSettings([Body] UpdateAiSettingsRequest request, CancellationToken cancellationToken = default);
}
