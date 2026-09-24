using System.Globalization;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Preferences;

namespace LogMyDay.Mcp.Contracts;

/// <summary>The same UserDto shape the REST users endpoint returns, plus the argument checks the tools share.</summary>
public static class UserMapping
{
    public const int MinPasswordLength = 10;

    public static UserDto ToDto(User u) => new(
        u.Id,
        u.Email,
        u.DisplayName,
        u.IsAdmin,
        u.CreatedUtc,
        u.UpdatedUtc,
        u.Culture,
        u.TimeZone,
        ActivityFilterPreferences.NormalizeDisplayType(u.ActivityDisplayType),
        ActivityFilterPreferences.NormalizeActivitySortOrder(u.ActivitySortOrder),
        ActivityFilterPreferences.NormalizePeriodSort(u.ActivityPeriodSort));

    public static string RequireCulture(string culture)
    {
        try
        {
            // predefinedOnly: with ICU any well-formed tag would otherwise be accepted.
            var info = CultureInfo.GetCultureInfo(culture.Trim(), predefinedOnly: true);
            if (string.IsNullOrEmpty(info.Name))
            {
                throw new ArgumentException("culture must be a specific culture such as en-US or de-DE, not the invariant culture.");
            }

            return info.Name;
        }
        catch (CultureNotFoundException)
        {
            throw new ArgumentException($"'{culture}' is not a known culture; use a name such as en-US, de-DE or cs-CZ.");
        }
    }

    public static string RequireTimeZone(string timeZone)
    {
        var id = timeZone.Trim();
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
        {
            throw new ArgumentException($"'{timeZone}' is not a known time zone; use an IANA id such as Europe/Vienna or America/New_York.");
        }

        // Prefer the IANA spelling in storage, whatever alias the caller used.
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
    }

    public static string RequirePassword(string password, string argument)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            throw new ArgumentException($"{argument} must be at least {MinPasswordLength} characters long.");
        }

        return password;
    }

    public static string RequireEmail(string email)
    {
        var trimmed = email.Trim();
        if (trimmed.Length < 3 || !trimmed.Contains('@') || trimmed.StartsWith('@') || trimmed.EndsWith('@'))
        {
            throw new ArgumentException("email must be a valid e-mail address.");
        }

        return trimmed;
    }
}
