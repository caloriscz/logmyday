using System.Globalization;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// Parses the date and date-time strings tools accept. Dates are yyyy-MM-dd. Date-times are naive
/// local (yyyy-MM-ddTHH:mm[:ss], a space works too); a value with an offset or Z is converted
/// into the user's time zone, so an agent that thinks in UTC still lands on the right local time.
/// </summary>
public static class DateArguments
{
    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"
    ];

    public static DateOnly ParseDate(string text, string argument)
    {
        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        // A date-time is fine for a date argument; only the date part is used.
        if (TryParseNaive(text, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        throw new ArgumentException($"{argument} must be a date in yyyy-MM-dd form, got '{text}'.");
    }

    public static DateTime ParseDateTime(string text, string argument, TimeZoneInfo userZone)
    {
        var trimmed = text.Trim();
        if (TryParseNaive(trimmed, out var naive))
        {
            return naive;
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(offset, userZone).DateTime, DateTimeKind.Unspecified);
        }

        throw new ArgumentException($"{argument} must be yyyy-MM-ddTHH:mm (local) or an ISO-8601 date-time with offset, got '{text}'.");
    }

    /// <summary>The last tick of the given day, for inclusive "to" filters.</summary>
    public static DateTime EndOfDay(DateOnly date) => date.AddDays(1).ToDateTime(TimeOnly.MinValue).AddTicks(-1);

    /// <summary>
    /// The period a non-repeatable tag's rows are compared within — the same arithmetic the
    /// activity service uses (weeks start on Monday). Exact tags have no period.
    /// </summary>
    public static (DateTime Start, DateTime End) PeriodOf(TimeGranularity granularity, DateTime at)
    {
        return granularity switch
        {
            TimeGranularity.Daily => (at.Date, at.Date.AddDays(1).AddTicks(-1)),
            TimeGranularity.Hourly => (HourStart(at), HourStart(at).AddHours(1).AddTicks(-1)),
            TimeGranularity.Weekly => (WeekStart(at), WeekStart(at).AddDays(7).AddTicks(-1)),
            TimeGranularity.Monthly => (new DateTime(at.Year, at.Month, 1), new DateTime(at.Year, at.Month, 1).AddMonths(1).AddTicks(-1)),
            TimeGranularity.Yearly => (new DateTime(at.Year, 1, 1), new DateTime(at.Year, 1, 1).AddYears(1).AddTicks(-1)),
            _ => (at, at)
        };
    }

    private static bool TryParseNaive(string text, out DateTime value)
    {
        return DateTime.TryParseExact(text.Trim(), DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static DateTime HourStart(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, 0, 0);

    private static DateTime WeekStart(DateTime at)
    {
        var diff = (7 + (at.DayOfWeek - DayOfWeek.Monday)) % 7;

        return at.AddDays(-diff).Date;
    }
}
