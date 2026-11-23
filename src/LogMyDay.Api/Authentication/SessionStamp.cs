using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Authentication;

/// <summary>
/// A fingerprint of what a sign-in cookie was issued for: the account's password and its admin
/// right. The cookie carries it, and the cookie is valid only while the account still has the
/// same fingerprint. A changed or reset password, or a changed admin right, ends every session of
/// the account: the user signs in again and gets the account's current rights.
/// </summary>
public static class SessionStamp
{
    public const string ClaimType = "lmd_session";

    /// <summary>The fingerprint of the account as it is now.</summary>
    public static string For(User user)
    {
        // A hash of the password hash: the cookie is encrypted, but it needn't carry the hash itself.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{user.PasswordHash}\n{user.IsAdmin}"));

        return Convert.ToBase64String(hash);
    }

    /// <summary>Whether the principal was signed in with the account as it is now.</summary>
    public static bool Matches(ClaimsPrincipal principal, User user)
    {
        return string.Equals(principal.FindFirstValue(ClaimType), For(user), StringComparison.Ordinal);
    }
}
