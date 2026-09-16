using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The API returns reminders regardless of their monitoring window, so every surface — including
/// the mobile notification scheduler and the fire-time alarm check — relies on this predicate to
/// decide what is active. A reminder that stayed "within window" past its end date kept firing
/// notifications while hidden from the list.
/// </summary>
public class ReminderMonitoringWindowTests
{
    private static ReminderResponse Reminder(string? from, string? to) => new()
    {
        Id = 1,
        Title = "Vitamin D",
        MonitorFromDate = from == null ? null : DateOnly.Parse(from),
        MonitorToDate = to == null ? null : DateOnly.Parse(to)
    };

    [Theory]
    // No window at all — always active.
    [InlineData(null, null, "2026-08-01", true)]
    // Start bound only.
    [InlineData("2026-07-01", null, "2026-06-30", false)]
    [InlineData("2026-07-01", null, "2026-07-01", true)]
    [InlineData("2026-07-01", null, "2026-12-31", true)]
    // End bound only — must close the window even with no start date.
    [InlineData(null, "2026-07-20", "2026-07-20", true)]
    [InlineData(null, "2026-07-20", "2026-07-21", false)]
    // Both bounds, inclusive on each end.
    [InlineData("2026-07-01", "2026-07-20", "2026-06-30", false)]
    [InlineData("2026-07-01", "2026-07-20", "2026-07-01", true)]
    [InlineData("2026-07-01", "2026-07-20", "2026-07-20", true)]
    [InlineData("2026-07-01", "2026-07-20", "2026-07-21", false)]
    public void IsWithinMonitoringWindow_HonoursBothBoundsIndependently(string? from, string? to, string date, bool expected)
    {
        var item = Reminder(from, to);

        Assert.Equal(expected, item.IsWithinMonitoringWindow(DateOnly.Parse(date)));
    }
}
