using LogMyDay.Api.Application.Options;

namespace LogMyDay.Api.Application.Interfaces;

/// <summary>
/// Service for managing application settings stored in the database.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current AI configuration from the database, falling back to appsettings if not found.
    /// </summary>
    Task<AiOptions> GetAiOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the AI configuration in the database.
    /// </summary>
    Task UpdateAiOptionsAsync(AiOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a setting value by key.
    /// </summary>
    Task SetSettingAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default);
}
