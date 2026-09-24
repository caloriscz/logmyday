using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>
/// A per-user API key for machine clients (the MCP server). The token itself is never stored:
/// <see cref="KeyHash"/> is its SHA-256 and <see cref="Prefix"/> the leading characters kept for
/// display and lookup. A key is dead once <see cref="RevokedUtc"/> is set or <see cref="ExpiresUtc"/>
/// has passed; rows are kept so the owner can see what existed.
/// </summary>
public sealed class ApiKey
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Prefix { get; set; }
    public required string KeyHash { get; set; }
    public ApiKeyScope Scope { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }

    // Navigation property
    public User? User { get; set; }
}
