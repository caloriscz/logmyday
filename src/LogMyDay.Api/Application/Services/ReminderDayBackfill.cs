using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// One-time migration of legacy per-reminder done/skip scalars into per-day <see cref="ReminderDay"/>
/// rows. Runs at startup; guarded by a <see cref="Setting"/> flag so it executes exactly once. Uses
/// the same timezone resolution as <see cref="ReminderService"/> so backfilled dates match how the
/// app computes a reminder's local day (doing this in raw migration SQL would re-introduce the
/// midnight-boundary bug this whole change fixes).
/// </summary>
public sealed class ReminderDayBackfill : IReminderDayBackfill
{
    private const string FlagKey = "reminderday.backfill.v1";

    private readonly LogMyDayDbContext _context;
    private readonly ILogger<ReminderDayBackfill> _logger;

    public ReminderDayBackfill(LogMyDayDbContext context, ILogger<ReminderDayBackfill> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var alreadyRun = await _context.Settings.AnyAsync(s => s.Key == FlagKey, cancellationToken);
        if (alreadyRun)
        {
            return;
        }

        var reminders = await _context.Reminders
            .Where(r => r.DoneAt != null || r.SkippedAt != null)
            .ToListAsync(cancellationToken);

        var users = await _context.Users
            .AsNoTracking()
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var created = 0;
        foreach (var reminder in reminders)
        {
            users.TryGetValue(reminder.UserId, out var user);
            var tz = ResolveTimeZone(user);

            // One row per period date for this reminder; merge done + skip if they land the same day.
            var rows = new Dictionary<DateOnly, ReminderDay>();

            if (reminder.DoneAt is { } doneAt)
            {
                var date = PeriodDate(reminder, doneAt, tz, user);
                var row = GetOrAdd(rows, reminder, date);
                row.IsDone = true;
                row.DoneAt = doneAt;
            }

            if (reminder.SkippedAt is { } skippedAt)
            {
                var date = PeriodDate(reminder, skippedAt, tz, user);
                var row = GetOrAdd(rows, reminder, date);
                row.IsSkipped = true;
                row.SkippedAt = skippedAt;
            }

            _context.ReminderDays.AddRange(rows.Values);
            created += rows.Count;
        }

        _context.Settings.Add(new Setting
        {
            Key = FlagKey,
            Value = "done",
            Description = "ReminderDay backfill from legacy DoneAt/SkippedAt completed"
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ReminderDay backfill complete: created {Count} day rows from {Reminders} reminders",
            created, reminders.Count);
    }

    private static ReminderDay GetOrAdd(Dictionary<DateOnly, ReminderDay> rows, Reminder reminder, DateOnly date)
    {
        if (!rows.TryGetValue(date, out var row))
        {
            row = new ReminderDay { ReminderId = reminder.Id, UserId = reminder.UserId, Date = date };
            rows[date] = row;
        }

        return row;
    }

    private static DateOnly PeriodDate(Reminder reminder, DateTime utc, TimeZoneInfo tz, User? user)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, tz));

        if (reminder.RecurrenceType == RecurrenceType.Weekly)
        {
            return GetWeekStart(localDate, ResolveFirstDayOfWeek(user));
        }

        return localDate;
    }

    private static TimeZoneInfo ResolveTimeZone(User? user)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DayOfWeek ResolveFirstDayOfWeek(User? user)
    {
        try
        {
            return new CultureInfo(user?.Culture ?? "en-US").DateTimeFormat.FirstDayOfWeek;
        }
        catch (CultureNotFoundException)
        {
            return DayOfWeek.Monday;
        }
    }

    private static DateOnly GetWeekStart(DateOnly date, DayOfWeek firstDay)
    {
        var diff = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        return date.AddDays(-diff);
    }
}
