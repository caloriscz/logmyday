using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

/// <summary>The user's local calendar day, from the time zone in their settings (UTC when unset
/// or unknown).</summary>
public static class UserDay
{
    public static async Task<DateOnly> Today(IUserDirectory users, Guid userId)
    {
        return DateOnly.FromDateTime(await Now(users, userId));
    }

    /// <summary>The user's local wall-clock time, as activities store it (unspecified kind).</summary>
    public static async Task<DateTime> Now(IUserDirectory users, Guid userId)
    {
        var timeZone = await users.GetTimeZone(userId);

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz), DateTimeKind.Unspecified);
    }
}
