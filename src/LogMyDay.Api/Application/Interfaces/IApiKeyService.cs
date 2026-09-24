using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

/// <summary>Outcome of a successful <see cref="IApiKeyService.Validate"/>: who the key acts as, and what it may do.</summary>
public sealed record ApiKeyValidation(User User, int KeyId, string Prefix, ApiKeyScope Scope);

public interface IApiKeyService
{
    /// <summary>Creates a key for the user. The raw token is in the result and is never recoverable again.</summary>
    Task<ApiKeyCreatedDto> Create(Guid userId, string name, ApiKeyScope scope, DateTime? expiresUtc, CancellationToken cancellationToken);

    Task<IList<ApiKeyDto>> List(Guid userId, CancellationToken cancellationToken);

    /// <summary>Revokes a key the user owns. Revoking an already revoked key is a no-op.</summary>
    Task Revoke(Guid userId, int keyId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a presented token. Null for anything that must not authenticate — malformed, unknown,
    /// hash mismatch, revoked, expired, or the owning user gone. Records last use, throttled so a busy
    /// key does not write on every request.
    /// </summary>
    Task<ApiKeyValidation?> Validate(string rawToken, CancellationToken cancellationToken);
}
