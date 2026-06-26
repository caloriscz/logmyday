using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// One-time conversion of legacy <see cref="RecurrenceType.None"/> reminders to
/// <see cref="RecurrenceType.Daily"/>. None was a remnant of the shared Basic/Reminder todo table
/// and is no longer offered. Existing <see cref="ReminderDay"/> rows are keyed by calendar day
/// (identical to Daily), so no completion state is lost. Runs at startup; guarded by a
/// <see cref="Setting"/> flag so it executes exactly once.
/// </summary>
public sealed class ReminderRecurrenceMigration : IReminderRecurrenceMigration
{
    private const string FlagKey = "reminder.recurrence.none-to-daily.v1";

    private readonly LogMyDayDbContext _context;
    private readonly ILogger<ReminderRecurrenceMigration> _logger;

    public ReminderRecurrenceMigration(LogMyDayDbContext context, ILogger<ReminderRecurrenceMigration> logger)
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

        var noneReminders = await _context.Reminders
            .Where(r => r.RecurrenceType == RecurrenceType.None)
            .ToListAsync(cancellationToken);

        foreach (var reminder in noneReminders)
        {
            reminder.RecurrenceType = RecurrenceType.Daily;
        }

        _context.Settings.Add(new Setting
        {
            Key = FlagKey,
            Value = "done",
            Description = "Converted legacy None-recurrence reminders to Daily"
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reminder recurrence migration complete: converted {Count} None reminders to Daily",
            noneReminders.Count);
    }
}
