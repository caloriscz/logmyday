using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

/// <summary>The user's local calendar day, from the time zone in their settings (UTC when unset
/// or unknown).</summary>
public static class UserDay
{
    public static async Task<DateOnly> Today(LogMyDayDbContext context, Guid userId)
    {
        var timeZone = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZone)
            .FirstOrDefaultAsync();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }
}
