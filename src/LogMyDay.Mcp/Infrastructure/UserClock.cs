using LogMyDay.Api.Application.Interfaces;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// "Now" and "today" as the user sees them. Activities are stored as naive local date-times, so a
/// default timestamp must come from the user's time zone, never the server's.
/// </summary>
public sealed class UserClock(IUserService users, TimeProvider clock)
{
    private TimeZoneInfo? _zone;

    public async Task<TimeZoneInfo> Zone(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_zone != null)
        {
            return _zone;
        }

        var user = await users.Get(userId, cancellationToken);
        _zone = Resolve(user?.TimeZone);

        return _zone;
    }

    public async Task<DateTime> Now(Guid userId, CancellationToken cancellationToken = default)
    {
        var zone = await Zone(userId, cancellationToken);

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, zone), DateTimeKind.Unspecified);
    }

    public async Task<DateOnly> Today(Guid userId, CancellationToken cancellationToken = default)
    {
        return DateOnly.FromDateTime(await Now(userId, cancellationToken));
    }

    public static TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
