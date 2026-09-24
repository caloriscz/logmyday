using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Per-user API keys for machine clients. The token is "lmd_" plus 32 random bytes, base64url; only
/// its SHA-256 and a display prefix are stored, so a database read never yields a usable credential.
/// The token string itself appears in exactly one place: the creation result.
/// </summary>
public sealed class ApiKeyService : IApiKeyService
{
    public const string TokenPrefix = "lmd_";
    public const int TokenLength = 47;          // "lmd_" + 43 chars of base64url for 32 bytes
    public const int PrefixLength = 12;         // "lmd_" + 8 — enough to tell keys apart in a list
    public const int MaxActiveKeysPerUser = 20;
    public const int MaxNameLength = 100;

    /// <summary>A busy key would otherwise write LastUsedUtc on every request.</summary>
    public static readonly TimeSpan LastUsedWriteInterval = TimeSpan.FromMinutes(5);

    private const int TokenRandomBytes = 32;

    private readonly LogMyDayDbContext _context;
    private readonly TimeProvider _time;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(LogMyDayDbContext context, TimeProvider time, ILogger<ApiKeyService> logger)
    {
        _context = context;
        _time = time;
        _logger = logger;
    }

    public async Task<ApiKeyCreatedDto> Create(Guid userId, string name, ApiKeyScope scope, DateTime? expiresUtc, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        name = name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            throw new ArgumentException("A key needs a name so it can be told apart from the others.", nameof(name));
        }

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Key name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        if (expiresUtc.HasValue && expiresUtc.Value <= now)
        {
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresUtc));
        }

        var activeCount = await _context.ApiKeys
            .CountAsync(k => k.UserId == userId && k.RevokedUtc == null && (k.ExpiresUtc == null || k.ExpiresUtc > now), cancellationToken);

        if (activeCount >= MaxActiveKeysPerUser)
        {
            throw new InvalidOperationException($"You already have {MaxActiveKeysPerUser} active keys. Revoke one before creating another.");
        }

        var token = GenerateToken();

        var key = new ApiKey
        {
            UserId = userId,
            Name = name,
            Prefix = token[..PrefixLength],
            KeyHash = Hash(token),
            Scope = scope,
            CreatedUtc = now,
            ExpiresUtc = expiresUtc
        };

        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("API key {Prefix} created for user {UserId} with scope {Scope}", key.Prefix, userId, scope);

        return new ApiKeyCreatedDto(MapToDto(key), token);
    }

    public async Task<IList<ApiKeyDto>> List(Guid userId, CancellationToken cancellationToken)
    {
        var keys = await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedUtc)
            .ToListAsync(cancellationToken);

        return keys.Select(MapToDto).ToList();
    }

    public async Task Revoke(Guid userId, int keyId, CancellationToken cancellationToken)
    {
        var key = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, cancellationToken);

        if (key == null)
        {
            throw new KeyNotFoundException("API key not found");
        }

        if (key.RevokedUtc.HasValue)
        {
            return;
        }

        key.RevokedUtc = _time.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("API key {Prefix} revoked for user {UserId}", key.Prefix, userId);
    }

    public async Task<ApiKeyValidation?> Validate(string rawToken, CancellationToken cancellationToken)
    {
        if (!LooksLikeToken(rawToken))
        {
            return null;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var prefix = rawToken[..PrefixLength];
        var presentedHash = Encoding.UTF8.GetBytes(Hash(rawToken));

        // The prefix is only a narrowing hint; the hash decides, and a constant-time compare keeps
        // the decision from leaking how close a guess was. Revoked keys are filtered here so a
        // revoked-then-reissued prefix cannot resurrect the old token.
        var candidates = await _context.ApiKeys
            .Include(k => k.User)
            .Where(k => k.Prefix == prefix && k.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        var key = candidates.FirstOrDefault(k =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(k.KeyHash), presentedHash));

        if (key == null)
        {
            return null;
        }

        if (key.ExpiresUtc.HasValue && key.ExpiresUtc.Value <= now)
        {
            return null;
        }

        if (key.User == null)
        {
            return null;
        }

        if (key.LastUsedUtc == null || now - key.LastUsedUtc.Value >= LastUsedWriteInterval)
        {
            key.LastUsedUtc = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ApiKeyValidation(key.User, key.Id, key.Prefix, key.Scope);
    }

    private static bool LooksLikeToken(string? token)
    {
        return token is { Length: TokenLength } && token.StartsWith(TokenPrefix, StringComparison.Ordinal);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenRandomBytes);

        return TokenPrefix + Base64Url.EncodeToString(bytes);
    }

    /// <summary>SHA-256 of the whole token, base64. No salt: the token is already 256 random bits.</summary>
    public static string Hash(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static ApiKeyDto MapToDto(ApiKey key)
    {
        return new ApiKeyDto(
            key.Id,
            key.Name,
            key.Prefix,
            key.Scope.ToString(),
            key.CreatedUtc,
            key.ExpiresUtc,
            key.RevokedUtc,
            key.LastUsedUtc);
    }
}
