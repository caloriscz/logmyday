namespace LogMyDay.UI.Services;

/// <summary>The times a reminder or todo completed through its record form sends, for the day the
/// page shows. The server stores the record in the user's time zone and refuses a completion whose
/// DoneAt falls on another of the user's days, so both come from one clock: today the current
/// instant, another day its noon. Mixing clocks (the app's date for DoneAt, the instant for the
/// record) split them around midnight when the app runs in another time zone than the user, such
/// as a web server in UTC.</summary>
public static class RecordCompletionTime
{
    /// <summary>The record form's default time.</summary>
    public static DateTime RecordTime(DateTime day)
    {
        return day.Date == DateTime.Today ? DateTime.Now : day.Date.AddHours(12);
    }

    /// <summary>The completion's DoneAt, in UTC, for a record saved from that form.</summary>
    public static DateTime DoneAt(DateTime day)
    {
        return day.Date == DateTime.Today
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(day.Date.AddHours(12), DateTimeKind.Local).ToUniversalTime();
    }
}
