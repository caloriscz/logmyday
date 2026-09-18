namespace LogMyDay.Shared.DTOs;

/// <summary>A key as listed to its owner. Never carries the token or its hash.</summary>
public record ApiKeyDto(
    int Id,
    string Name,
    string Prefix,
    string Scope,
    DateTime CreatedUtc,
    DateTime? ExpiresUtc,
    DateTime? RevokedUtc,
    DateTime? LastUsedUtc);

public record CreateApiKeyDto(string Name, string Scope, DateTime? ExpiresUtc);

/// <summary>Returned once, on creation. <see cref="Token"/> is not recoverable afterwards.</summary>
public record ApiKeyCreatedDto(ApiKeyDto Key, string Token);
