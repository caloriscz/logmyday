using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.Formatting;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class TagTriggerEngine : ITagTriggerEngine
{
    /// <summary>A trigger's reminders this many days past their day are deleted when it next fires.</summary>
    public const int KeepDays = 30;

    private readonly LogMyDayDbContext _context;
    private readonly IEventLogService _eventLogService;

    public TagTriggerEngine(LogMyDayDbContext context, IEventLogService eventLogService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
    }

    public async Task OnTagsChanged(Guid userId, IReadOnlyCollection<(int TagId, DateOnly Day)> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var changed = await WithDerivedChanges(userId, changes);
        var changedIds = changed.Select(c => c.TagId).Distinct().ToList();
        var triggers = await _context.TagTriggers
            .Include(t => t.IfTag).ThenInclude(t => t!.Group)
            .Include(t => t.PromptTag).ThenInclude(t => t!.Group)
            .Where(t => t.UserId == userId && t.IsEnabled && (changedIds.Contains(t.IfTagId) || changedIds.Contains(t.PromptTagId)))
            .ToListAsync();
        if (triggers.Count == 0)
        {
            return;
        }

        var now = await UserDay.Now(_context, userId);
        var log = new List<string>();
        foreach (var trigger in triggers)
        {
            foreach (var day in changed.Where(c => c.TagId == trigger.IfTagId).Select(c => c.Day).Distinct())
            {
                await EvaluateDay(trigger, day, now, log);
            }

            foreach (var day in changed.Where(c => c.TagId == trigger.PromptTagId).Select(c => c.Day).Distinct())
            {
                await CloseWhenLogged(trigger, day, log);
            }
        }

        await _context.SaveChangesAsync();

        foreach (var message in log)
        {
            await _eventLogService.Log(userId, EventLogLevel.Info, message);
        }
    }

    /// <summary>When a reminder for this trigger rings: the entry time plus the offset, or the
    /// first time of day after the entry (that day or the next), to the minute.</summary>
    public static DateTime FireTime(TagTrigger trigger, DateTime anchor)
    {
        var fire = trigger.Timing == TagTriggerTiming.AtTime && trigger.AtTime is TimeOnly at
            ? NextAt(anchor, at)
            : anchor.AddMinutes(trigger.OffsetMinutes ?? 0);

        return new DateTime(fire.Year, fire.Month, fire.Day, fire.Hour, fire.Minute, 0, DateTimeKind.Unspecified);
    }

    private static DateTime NextAt(DateTime anchor, TimeOnly at)
    {
        var sameDay = anchor.Date + at.ToTimeSpan();

        return sameDay > anchor ? sameDay : sameDay.AddDays(1);
    }

    // A computed tag changes with its sources (the rules have already run), and a saved record
    // counts as its record type being logged, for a trigger that prompts for the record type.
    private async Task<List<(int TagId, DateOnly Day)>> WithDerivedChanges(Guid userId, IReadOnlyCollection<(int TagId, DateOnly Day)> changes)
    {
        var tagIds = changes.Select(c => c.TagId).Distinct().ToList();
        var all = changes.ToList();

        var rules = await _context.TagRules
            .Where(r => r.UserId == userId && r.IsEnabled && r.Sources.Any(s => tagIds.Contains(s.SourceTagId)))
            .Select(r => new { r.TargetTagId, r.EffectiveFrom, Sources = r.Sources.Select(s => s.SourceTagId).ToList() })
            .ToListAsync();
        foreach (var rule in rules)
        {
            all.AddRange(changes
                .Where(c => rule.Sources.Contains(c.TagId) && c.Day >= rule.EffectiveFrom)
                .Select(c => (rule.TargetTagId, c.Day)));
        }

        var fields = await _context.Tags
            .Where(t => tagIds.Contains(t.Id) && t.RecordTypeId != null)
            .Select(t => new { t.Id, RecordTypeId = t.RecordTypeId!.Value })
            .ToListAsync();
        foreach (var field in fields)
        {
            all.AddRange(changes.Where(c => c.TagId == field.Id).Select(c => (field.RecordTypeId, c.Day)));
        }

        return all.Distinct().ToList();
    }

    private async Task EvaluateDay(TagTrigger trigger, DateOnly day, DateTime now, List<string> log)
    {
        var entries = await EntriesOf(trigger.UserId, trigger.IfTagId, day);
        var (holds, value) = TagCheckEvaluator.Condition(trigger.IfOperator, trigger.IfOperand, trigger.IfTag!, entries);
        var existing = await _context.Reminders
            .Include(r => r.Days)
            .FirstOrDefaultAsync(r => r.TriggerId == trigger.Id && r.TriggerDay == day);

        if (!holds)
        {
            // Withdraw an open reminder; a done one stays as history.
            if (existing != null && !IsDone(existing))
            {
                _context.Reminders.Remove(existing);
                log.Add($"Trigger '{trigger.Name}' withdrew its reminder for {day:yyyy-MM-dd}: {Condition(trigger)} no longer holds");
            }

            return;
        }

        // One reminder per trigger and day; later entries of the first tag do not move it.
        if (existing != null)
        {
            return;
        }

        var anchor = await AnchorOf(trigger, day, entries);
        var fire = FireTime(trigger, anchor);

        // A first tag logged after the fact (its reminder time already past) reminds of nothing.
        if (fire <= now)
        {
            return;
        }

        var fireDay = DateOnly.FromDateTime(fire);
        _context.Reminders.Add(new Reminder
        {
            UserId = trigger.UserId,
            Title = trigger.Name,
            NotifyAt = TimeOnly.FromDateTime(fire),
            RecurrenceType = RecurrenceType.Daily,
            MonitorFromDate = fireDay,
            MonitorToDate = fireDay,
            CompletionTagId = trigger.PromptTagId,
            AutoLogMode = AutoLogMode.Add,
            TriggerId = trigger.Id,
            TriggerDay = day,
            DateCreated = DateTime.UtcNow
        });
        log.Add($"Trigger '{trigger.Name}' set a reminder to log '{trigger.PromptTag!.TagName}' at {fire:yyyy-MM-dd HH:mm} ({Condition(trigger)}: {TagCheckText.Value(value ?? "logged")} at {anchor:HH:mm})");

        await RemoveOldReminders(trigger, DateOnly.FromDateTime(now));
    }

    // An open reminder of the trigger closes when its prompted tag gets a real entry from the
    // trigger's day to the reminder's day, saved after the reminder was set. An entry saved
    // earlier (last night's sleep logged in the morning) answers nothing. A skipped reminder
    // stays skipped: skipping writes its own placeholder entry to the prompted tag.
    private async Task CloseWhenLogged(TagTrigger trigger, DateOnly day, List<string> log)
    {
        var reminders = await _context.Reminders
            .Include(r => r.Days)
            .Where(r => r.TriggerId == trigger.Id && r.TriggerDay <= day && r.MonitorToDate >= day)
            .ToListAsync();

        foreach (var reminder in reminders.Where(r => !r.Days.Any(d => d.IsDone || d.IsSkipped)))
        {
            var from = reminder.TriggerDay!.Value.ToDateTime(TimeOnly.MinValue);
            var to = reminder.MonitorToDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var prompt = trigger.PromptTag!;

            bool logged;
            if (prompt.IsRecordType)
            {
                logged = await _context.Records.AnyAsync(r => r.UserId == trigger.UserId && r.RecordTypeId == prompt.Id
                    && r.DateStarted >= from && r.DateStarted < to && r.DateCreated >= reminder.DateCreated);
            }
            else
            {
                var rows = await _context.Activities
                    .AsNoTracking()
                    .Where(a => a.UserId == trigger.UserId && a.TagId == prompt.Id
                        && a.DateStarted >= from && a.DateStarted < to && a.DateCreated >= reminder.DateCreated)
                    .Select(a => new { a.Id, a.DateStarted, a.Description })
                    .ToListAsync();
                logged = rows.Any(r => TagCheckEvaluator.IsReal(prompt, new TagDayValue.Entry(r.Id, r.DateStarted, r.Description)));
            }

            if (!logged)
            {
                continue;
            }

            var reminderDay = reminder.MonitorFromDate!.Value;
            var row = reminder.Days.FirstOrDefault(d => d.Date == reminderDay);
            if (row == null)
            {
                row = new ReminderDay { UserId = reminder.UserId, ReminderId = reminder.Id, Date = reminderDay };
                _context.ReminderDays.Add(row);
            }

            var doneAt = DateTime.UtcNow;
            row.IsDone = true;
            row.DoneAt = doneAt;
            row.IsSkipped = false;
            row.SkippedAt = null;

            // Legacy scalars kept in sync as ReminderService.Complete does.
            reminder.IsDone = true;
            reminder.DoneAt = doneAt;
            log.Add($"Trigger '{trigger.Name}' closed its reminder for {reminderDay:yyyy-MM-dd}: '{prompt.TagName}' was logged");
        }
    }

    // Reminders long past their day only fill the table.
    private async Task RemoveOldReminders(TagTrigger trigger, DateOnly today)
    {
        var cutoff = today.AddDays(-KeepDays);
        var old = await _context.Reminders
            .Where(r => r.TriggerId == trigger.Id && r.MonitorToDate < cutoff)
            .ToListAsync();
        _context.Reminders.RemoveRange(old);
    }

    private async Task<List<TagDayValue.Entry>> EntriesOf(Guid userId, int tagId, DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.TagId == tagId && a.DateStarted >= start && a.DateStarted < end)
            .Select(a => new { a.Id, a.DateStarted, a.Description })
            .ToListAsync();

        return rows.Select(r => new TagDayValue.Entry(r.Id, r.DateStarted, r.Description)).ToList();
    }

    // The entry the reminder counts from: the latest real one of the day, or the latest of any
    // when the condition holds on skip markers only (e.g. "= 0"). A computed tag's value is
    // stored at the start of its day, so for it the latest entry of its rule's sources counts.
    private async Task<DateTime> AnchorOf(TagTrigger trigger, DateOnly day, IReadOnlyCollection<TagDayValue.Entry> entries)
    {
        if (trigger.IfTag!.IsComputed)
        {
            var sources = await _context.TagRules
                .Where(r => r.TargetTagId == trigger.IfTagId)
                .SelectMany(r => r.Sources.Select(s => s.SourceTagId))
                .ToListAsync();
            var start = day.ToDateTime(TimeOnly.MinValue);
            var end = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var latest = await _context.Activities
                .Where(a => a.UserId == trigger.UserId && sources.Contains(a.TagId) && a.DateStarted >= start && a.DateStarted < end)
                .MaxAsync(a => (DateTime?)a.DateStarted);
            if (latest is DateTime at)
            {
                return at;
            }
        }

        var real = entries.Where(e => TagCheckEvaluator.IsReal(trigger.IfTag, e)).ToList();

        return (real.Count > 0 ? real : entries).Max(e => e.DateStarted);
    }

    private static bool IsDone(Reminder reminder) => reminder.Days.Any(d => d.IsDone);

    private static string Condition(TagTrigger trigger) =>
        TagCheckText.Condition($"'{trigger.IfTag!.TagName}'", trigger.IfOperator.ToString(), trigger.IfOperand);
}
