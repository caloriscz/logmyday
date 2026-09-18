using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace LogMyDay.Api.Authentication;

/// <summary>
/// Remembers, briefly, that a password has already been verified against a given stored hash.
///
/// HTTP Basic re-sends credentials on every request, so without this the API runs a full Argon2id
/// derivation (m=64MB, t=3) per request, while the cookie scheme pays that once at sign-in. That
/// asymmetry is why the mobile client was slow across the board and the web app was not.
///
/// The security properties that matter:
///  - Only SUCCESSFUL verifications are recorded, so a wrong password still pays the full
///    derivation on every attempt and brute-force cost is unchanged.
///  - The stored hash forms part of the key, so changing a password (which replaces salt and hash)
///    cannot match any existing entry — a changed credential is rejected on the very next request.
///  - The presented password is never stored, only an HMAC of it under a key that lives and dies
///    with the process.
///  - Callers still load the user and still run lockout tracking; this only skips the derivation.
/// </summary>
public sealed class PasswordVerificationCache
{
    /// <summary>
    /// Long enough to collapse a burst of requests from one screen and a normal spell of
    /// interaction; short enough that a credential revoked by any means other than a password
    /// change cannot outlive it by much.
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private const string KeyPrefix = "pwverify_";

    private readonly IMemoryCache _cache;
    private readonly byte[] _macKey = RandomNumberGenerator.GetBytes(32);

    public PasswordVerificationCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsVerified(Guid userId, string storedHash, string password)
        => _cache.TryGetValue(BuildKey(userId, storedHash, password), out _);

    public void Record(Guid userId, string storedHash, string password)
    {
        _cache.Set(
            BuildKey(userId, storedHash, password),
            true,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Ttl,
                Size = 1
            });
    }

    private string BuildKey(Guid userId, string storedHash, string password)
    {
        var mac = HMACSHA256.HashData(_macKey, Encoding.UTF8.GetBytes(password));

        return $"{KeyPrefix}{userId}_{storedHash}_{Convert.ToBase64String(mac)}";
    }
}
