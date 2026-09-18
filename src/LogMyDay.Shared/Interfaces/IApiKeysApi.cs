using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

/// <summary>The caller's own API keys. Session or Basic auth only — a key cannot manage keys.</summary>
public interface IApiKeysApi
{
    [Get("/api/account/api-keys")]
    Task<IList<ApiKeyDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The returned token is shown once and is not recoverable afterwards.</summary>
    [Post("/api/account/api-keys")]
    Task<ApiKeyCreatedDto> CreateAsync([Body] CreateApiKeyDto request, CancellationToken cancellationToken = default);

    [Delete("/api/account/api-keys/{id}")]
    Task RevokeAsync(int id, CancellationToken cancellationToken = default);
}
