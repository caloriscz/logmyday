using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Authentication;

/// <summary>
/// The principal of a sign-in with the web app's cookie. The cookie says who signed in and with
/// which password; who the account is now, its admin right included, comes from the account on
/// every request. A changed or reset password ends the sign-ins made with the old one.
/// </summary>
public static class CookieSession
{
    public const string AuthenticationScheme = "lmd-cookie";

    /// <summary>A fingerprint of the password the sign-in was made with.</summary>
    public const string StampClaimType = "lmd_session";

    /// <summary>The principal of the account as it is now.</summary>
    public static ClaimsPrincipal Create(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new Claim("is_admin", user.IsAdmin.ToString().ToLowerInvariant()),
            new Claim(StampClaimType, Stamp(user))
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));
    }

    /// <summary>Whether the sign-in was made with the password the account has now.</summary>
    public static bool IsCurrent(ClaimsPrincipal principal, User user)
    {
        return string.Equals(principal.FindFirstValue(StampClaimType), Stamp(user), StringComparison.Ordinal);
    }

    private static string Stamp(User user)
    {
        // A hash of the password hash, which needn't leave the server even in an encrypted cookie.
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)));
    }
}
