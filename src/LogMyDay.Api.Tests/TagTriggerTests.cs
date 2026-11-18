using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Formatting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

/// <summary>Tag Activity Relations triggers on a real SQLite database, through the real write paths
/// (activities, records, reminder completion): the reminder they set, withdraw and close,
/// validation, delete guards and backups.</summary>
public sealed class TagTriggerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LogMyDayDbContext _context;
    private readonly EventLogService _eventLog;
    private readonly TagTriggerService _triggers;
    private readonly ActivityService _activities;
    private readonly RecordTypeService _types;
    private readonly RecordService _records;
    private readonly Guid _userId = Guid.NewGuid();

    public TagTriggerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.Users.Add(new User { Id = _userId, Email = "t@t", PasswordHash = "x", TimeZone = "UTC", Culture = "en-US" });
        _context.SaveChanges();

        _eventLog = new EventLogService(_context, NullLogger<EventLogService>.Instance);
        _triggers = new TagTriggerService(_context, _eventLog);
        _activities = new ActivityService(_context, new ActivityRepository(_context), _eventLog, new TagDayLockService(_context));
        _types = new RecordTypeService(_context, _eventLog);
        _records = new RecordService(_context, _eventLog, new TagRuleEngine(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // The user's time zone is UTC, so local wall-clock time is UTC without a kind.
    private static DateTime Now => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private static DateTime Minute(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0);

    // Exact by default, so a reminder counts from the entry's own time and the tests can name it.
    private async Task<Tag> AddTag(string name, int inputTypeId, bool repeatable = true, TimeGranularity granularity = TimeGranularity.Exact)
    {
        var tag = new Tag
        {
            TagName = name,
            InputTypeId = inputTypeId,
            IsRepeatable = repeatable,
            TimeGranularity = granularity,
            UserId = _userId
        };
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return tag;
    }

    private Task<ActivityResponse> Log(Tag tag, DateTime at, string value) =>
        _activities.Create(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = at, Description = value }, _userId);

    private Task<TagTriggerResponse> Trigger(Tag ifTag, Tag promptTag, TagRuleOperator op = TagRuleOperator.IsLogged,
        string? operand = null, int? offsetMinutes = 180, TimeOnly? atTime = null, string name = "Log the effect") =>
        _triggers.Create(new TagTriggerRequest
        {
            Name = name,
            IfTagId = ifTag.Id,
            IfOperator = op,
            IfOperand = operand,
            PromptTagId = promptTag.Id,
            Timing = atTime == null ? TagTriggerTiming.AfterEntry : TagTriggerTiming.AtTime,
            OffsetMinutes = atTime == null ? offsetMinutes : null,
            AtTime = atTime
        }, _userId);

    private async Task<List<Reminder>> RemindersOf(TagTriggerResponse trigger)
    {
        _context.ChangeTracker.Clear();

        return await _context.Reminders.AsNoTracking().Include(r => r.Days)
            .Where(r => r.TriggerId == trigger.Id)
            .ToListAsync();
    }

    [Fact]
    public async Task AnEntry_SetsOneReminderForItsDay_AfterTheOffset()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var trigger = await Trigger(dose, effect);
        var anchor = Minute(Now.AddMinutes(-10));

        await Log(dose, anchor, "100");

        var reminder = Assert.Single(await RemindersOf(trigger));
        var fire = anchor.AddMinutes(180);
        Assert.Equal(("Log the effect", effect.Id, RecurrenceType.Daily), (reminder.Title, reminder.CompletionTagId, reminder.RecurrenceType));
        Assert.Equal(TimeOnly.FromDateTime(fire), reminder.NotifyAt);
        Assert.Equal((DateOnly.FromDateTime(fire), DateOnly.FromDateTime(fire)), (reminder.MonitorFromDate, reminder.MonitorToDate));
        Assert.Equal(DateOnly.FromDateTime(anchor), reminder.TriggerDay);
        Assert.Null(reminder.Notes);

        // A second entry the same day neither adds a reminder nor moves it.
        await Log(dose, anchor.AddMinutes(5), "50");
        Assert.Equal(TimeOnly.FromDateTime(fire), Assert.Single(await RemindersOf(trigger)).NotifyAt);
        Assert.True(await _context.EventLogs.AnyAsync(e => e.Message.Contains("set a reminder to log 'Effect'")));

        // The Reminders list shows it with its trigger.
        var reminders = new ReminderService(_context, _activities, _eventLog, NullLogger<ReminderService>.Instance);
        var listed = Assert.Single(await reminders.GetAll(_userId, DateOnly.FromDateTime(fire)));
        Assert.Equal((trigger.Id, "Log the effect"), (listed.TriggerId, listed.TriggerName));
        Assert.Equal(1, (await _triggers.GetById(trigger.Id, _userId)).OpenReminderCount);
    }

    [Fact]
    public async Task ATagWithoutExactTimes_CountsFromTheSave_ForAnEntryToday()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer, granularity: TimeGranularity.Daily);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var trigger = await Trigger(dose, effect, offsetMinutes: 180);

        // Completing a reminder stores today's entry at noon; the reminder counts from the save.
        var before = Minute(Now);
        await Log(dose, DateOnly.FromDateTime(Now).ToDateTime(new TimeOnly(12, 0)), "100");
        var after = Now;
        var reminder = Assert.Single(await RemindersOf(trigger));
        var fire = reminder.MonitorFromDate!.Value.ToDateTime(reminder.NotifyAt!.Value);
        Assert.InRange(fire, before.AddMinutes(180), after.AddMinutes(180));

        // An entry for another day keeps its own time, long past: nothing more.
        await Log(dose, DateOnly.FromDateTime(Now).AddDays(-2).ToDateTime(new TimeOnly(12, 0)), "100");
        Assert.Single(await RemindersOf(trigger));
    }

    [Fact]
    public async Task AValueCondition_AtATime_SetsTheNextMorningReminder_AndIsWithdrawnWhenItStopsHolding()
    {
        var alcohol = await AddTag("Alcohol", InputTypeIds.Boolean);
        var sleep = await AddTag("Sleep", InputTypeIds.Score);
        var anchor = Minute(Now.AddMinutes(-10));

        // A time of day before the entry's time rings the next day.
        var at = TimeOnly.FromDateTime(anchor.AddHours(-1));
        var trigger = await Trigger(alcohol, sleep, TagRuleOperator.IsYes, atTime: at, name: "Morning check");

        var entry = await Log(alcohol, anchor, "false");
        Assert.Empty(await RemindersOf(trigger));

        await _activities.Update(entry.Id, new ActivityRequest { PrimaryTagId = alcohol.Id, DateStarted = anchor, Description = "true" }, _userId);
        var reminder = Assert.Single(await RemindersOf(trigger));
        var expected = anchor.Date + at.ToTimeSpan();
        expected = expected > anchor ? expected : expected.AddDays(1);
        Assert.Equal((DateOnly.FromDateTime(expected), at), (reminder.MonitorFromDate!.Value, reminder.NotifyAt!.Value));

        // Back to "no": the open reminder is withdrawn.
        await _activities.Update(entry.Id, new ActivityRequest { PrimaryTagId = alcohol.Id, DateStarted = anchor, Description = "false" }, _userId);
        Assert.Empty(await RemindersOf(trigger));
        Assert.True(await _context.EventLogs.AnyAsync(e => e.Message.Contains("withdrew its reminder")));
    }

    [Fact]
    public async Task ASave_ReportsTheReminderItSet_Once_AndNotOneWithdrawnInTheSameRequest()
    {
        // One engine per request, shared by the service and the response, as in the host.
        var engine = new TagTriggerEngine(_context, _eventLog);
        var activities = new ActivityService(_context, new ActivityRepository(_context), _eventLog, new TagDayLockService(_context), tagTriggerEngine: engine);
        var alcohol = await AddTag("Alcohol", InputTypeIds.Boolean);
        var sleep = await AddTag("Sleep", InputTypeIds.Score);
        var anchor = Minute(Now.AddMinutes(-10));
        var at = TimeOnly.FromDateTime(anchor.AddHours(-1));
        var trigger = await Trigger(alcohol, sleep, TagRuleOperator.IsYes, atTime: at, name: "Morning check");
        Task<ActivityResponse> Save(int id, string value) =>
            activities.Update(id, new ActivityRequest { PrimaryTagId = alcohol.Id, DateStarted = anchor, Description = value }, _userId);

        // A save that sets nothing reports nothing.
        var entry = await activities.Create(new ActivityRequest { PrimaryTagId = alcohol.Id, DateStarted = anchor, Description = "false" }, _userId);
        Assert.Null(engine.TakeCreated());

        await Save(entry.Id, "true");
        var set = Assert.Single(engine.TakeCreated()!);
        var reminder = Assert.Single(await RemindersOf(trigger));
        Assert.Equal((reminder.Id, trigger.Id, "Morning check", "Sleep"), (set.ReminderId, set.TriggerId, set.TriggerName, set.PromptTagName));
        Assert.Equal((DateOnly.FromDateTime(anchor), reminder.MonitorFromDate!.Value, at), (set.EntryDay, set.Day, set.Time));

        // Taken once: the next response does not repeat it.
        Assert.Null(engine.TakeCreated());

        // Set again and withdrawn before the response: nothing to report.
        await Save(entry.Id, "false");
        await Save(entry.Id, "true");
        await Save(entry.Id, "false");
        Assert.Null(engine.TakeCreated());
        Assert.Empty(await RemindersOf(trigger));
    }

    [Fact]
    public void TheNotice_SaysWhatToLog_AndWhen_CountedFromTheEntrysDay()
    {
        var evening = new TriggeredReminder { PromptTagName = "Effect", EntryDay = new DateOnly(2026, 11, 16), Day = new DateOnly(2026, 11, 16), Time = new TimeOnly(21, 30) };
        var morning = new TriggeredReminder { PromptTagName = "sleep: quality", EntryDay = new DateOnly(2026, 11, 16), Day = new DateOnly(2026, 11, 17), Time = new TimeOnly(8, 0) };

        Assert.Equal("Log Effect at 21:30", TagTriggerText.ReminderSet(evening));
        Assert.Equal("Log sleep: quality the next day at 08:00", TagTriggerText.ReminderSet(morning));
    }

    [Fact]
    public async Task ATimeAlreadyPast_ASkipMarker_AndADeletedEntry_LeaveNoReminder()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var trigger = await Trigger(dose, effect, offsetMinutes: 60);

        // Logged after the fact: the reminder time has passed.
        await Log(dose, Now.AddHours(-3), "100");
        Assert.Empty(await RemindersOf(trigger));
        await _context.Activities.Where(a => a.TagId == dose.Id).ExecuteDeleteAsync();

        // A skip marker is not "logged".
        await Log(dose, Minute(Now.AddMinutes(-5)), "0");
        Assert.Empty(await RemindersOf(trigger));
        await _context.Activities.Where(a => a.TagId == dose.Id).ExecuteDeleteAsync();

        var entry = await Log(dose, Minute(Now.AddMinutes(-5)), "100");
        Assert.Single(await RemindersOf(trigger));

        await _activities.Delete(entry.Id, _userId);
        Assert.Empty(await RemindersOf(trigger));
    }

    [Fact]
    public async Task LoggingThePromptedTag_ClosesTheReminder_ButAnEntrySavedBeforeItDoesNot()
    {
        var caffeine = await AddTag("Caffeine", InputTypeIds.Integer);
        var sleep = await AddTag("Sleep", InputTypeIds.Score);
        var trigger = await Trigger(caffeine, sleep, offsetMinutes: 120);

        // Last night's sleep, logged this morning, before the trigger set anything.
        await Log(sleep, Minute(Now.AddMinutes(-30)), "4");
        await Log(caffeine, Minute(Now.AddMinutes(-10)), "1");
        var reminder = Assert.Single(await RemindersOf(trigger));
        Assert.Empty(reminder.Days);

        await Log(sleep, Minute(Now), "3");
        var closed = Assert.Single(await RemindersOf(trigger));
        var day = Assert.Single(closed.Days);
        Assert.True(day.IsDone);
        Assert.Equal(closed.MonitorFromDate, day.Date);
        Assert.Equal(0, (await _triggers.GetById(trigger.Id, _userId)).OpenReminderCount);

        // A done reminder stays when the condition stops holding.
        await _context.Activities.Where(a => a.TagId == caffeine.Id).ExecuteDeleteAsync();
        await Log(caffeine, Minute(Now.AddMinutes(-1)), "0");
        Assert.Single(await RemindersOf(trigger));
    }

    [Fact]
    public async Task CompletingTheReminder_LogsThePromptedTagOnce()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var trigger = await Trigger(dose, effect, offsetMinutes: 5);
        await Log(dose, Minute(Now), "100");
        var reminder = Assert.Single(await RemindersOf(trigger));

        var reminders = new ReminderService(_context, _activities, _eventLog, NullLogger<ReminderService>.Instance);
        var completed = await reminders.Complete(reminder.Id, new ReminderCompleteRequest { DoneAt = DateTime.UtcNow, CompletionValue = "Calm" }, _userId);

        Assert.True(completed.IsDone);
        Assert.Equal("Calm", Assert.Single(await _context.Activities.Where(a => a.TagId == effect.Id).ToListAsync()).Description);
        Assert.All((await RemindersOf(trigger)).Single().Days, d => Assert.True(d.IsDone));
    }

    [Fact]
    public async Task SkippingTheReminder_KeepsItSkipped()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var trigger = await Trigger(dose, effect, offsetMinutes: 5);
        await Log(dose, Minute(Now), "100");
        var reminder = Assert.Single(await RemindersOf(trigger));

        // Skipping logs an empty Effect entry; it must not turn the skip into "done".
        var reminders = new ReminderService(_context, _activities, _eventLog, NullLogger<ReminderService>.Instance);
        var skipped = await reminders.Skip(reminder.Id, _userId);

        Assert.True(skipped.IsSkipped);
        var day = Assert.Single((await RemindersOf(trigger)).Single().Days);
        Assert.Equal((true, false), (day.IsSkipped, day.IsDone));
    }

    [Fact]
    public async Task ARecordField_AndARecordType_WorkAsTheWatchedAndThePromptedTag()
    {
        var run = await _types.Create(new RecordTypeRequest
        {
            Name = "Run",
            Fields = [new() { Name = "Distance", TypeId = InputTypeIds.Decimal }]
        }, _userId);
        var recovery = await _types.Create(new RecordTypeRequest
        {
            Name = "Recovery",
            Fields = [new() { Name = "Soreness", TypeId = InputTypeIds.Score }]
        }, _userId);
        var distance = await _context.Tags.SingleAsync(t => t.Id == run.Fields[0].TagId);
        var recoveryType = await _context.Tags.SingleAsync(t => t.Id == recovery.Id);
        var trigger = await Trigger(distance, recoveryType, TagRuleOperator.GreaterOrEqual, "10", offsetMinutes: 60, name: "Log recovery");

        await _records.Create(new RecordRequest
        {
            RecordTypeId = run.Id,
            DateStarted = Minute(Now.AddMinutes(-10)),
            Values = [new() { TagId = distance.Id, Value = "12" }]
        }, _userId);
        Assert.Single(await RemindersOf(trigger));

        await _records.Create(new RecordRequest
        {
            RecordTypeId = recovery.Id,
            DateStarted = Minute(Now),
            Values = [new() { TagId = recovery.Fields[0].TagId, Value = "2" }]
        }, _userId);
        Assert.True(Assert.Single((await RemindersOf(trigger)).Single().Days).IsDone);
    }

    [Fact]
    public async Task ACheckOnAComputedTag_FiresWhenItsSourceIsSaved()
    {
        var espresso = await AddTag("Espresso", InputTypeIds.Integer);
        var total = await AddTag("Caffeine drinks", InputTypeIds.Integer, repeatable: false);
        var sleep = await AddTag("Sleep", InputTypeIds.Score);
        var rules = new TagRuleService(_context, new TagRuleEngine(_context), _eventLog);
        await rules.Create(new TagRuleRequest
        {
            Name = "Caffeine",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = espresso.Id, Factor = 1 }]
        }, _userId);
        var trigger = await Trigger(total, sleep, TagRuleOperator.GreaterOrEqual, "3", name: "Sleep after coffee");

        await Log(espresso, Minute(Now.AddMinutes(-20)), "2");
        Assert.Empty(await RemindersOf(trigger));

        var third = Minute(Now.AddMinutes(-10));
        await Log(espresso, third, "1");

        // Counted from the source entry, not from the computed value's start-of-day row.
        Assert.Equal(TimeOnly.FromDateTime(third.AddMinutes(180)), Assert.Single(await RemindersOf(trigger)).NotifyAt);
    }

    [Fact]
    public async Task Validation_RefusesTriggersThatCannotWork()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var type = await _types.Create(new RecordTypeRequest { Name = "Run", Fields = [new() { Name = "Distance", TypeId = InputTypeIds.Decimal }] }, _userId);
        var recordType = await _context.Tags.SingleAsync(t => t.Id == type.Id);
        var field = await _context.Tags.SingleAsync(t => t.Id == type.Fields[0].TagId);
        var computed = await AddTag("Total", InputTypeIds.Integer);
        computed.IsComputed = true;
        await _context.SaveChangesAsync();

        async Task<string> Refusal(Func<Task> create) => (await Assert.ThrowsAsync<ArgumentException>(create)).Message;

        Assert.Contains("different tag", await Refusal(() => Trigger(dose, dose)));
        Assert.Contains("record type", await Refusal(() => Trigger(recordType, effect)));
        Assert.Contains("computed", await Refusal(() => Trigger(dose, computed)));
        Assert.Contains("field of a record type", await Refusal(() => Trigger(dose, field)));
        Assert.Contains("5 minutes and 24 hours", await Refusal(() => Trigger(dose, effect, offsetMinutes: 2)));
        Assert.Contains("is yes", await Refusal(() => Trigger(dose, effect, TagRuleOperator.IsYes)));
        Assert.Contains("name", await Refusal(() => Trigger(dose, effect, name: " ")));
        await Assert.ThrowsAsync<ArgumentException>(() => _triggers.Create(new TagTriggerRequest
        {
            Name = "No time",
            IfTagId = dose.Id,
            PromptTagId = effect.Id,
            Timing = TagTriggerTiming.AtTime
        }, _userId));

        // A record type is a fine thing to be reminded of.
        Assert.True((await Trigger(field, recordType)).PromptIsRecordType);
    }

    [Fact]
    public async Task APromptedTag_CannotBecomeARuleTarget_BeforeTheTriggerFires()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.Integer);
        await Trigger(dose, effect, name: "Effect later");
        var rules = new TagRuleService(_context, new TagRuleEngine(_context), _eventLog);

        var refusal = await Assert.ThrowsAsync<ArgumentException>(() => rules.Create(new TagRuleRequest
        {
            Name = "Effect total",
            TargetTagId = effect.Id,
            Sources = [new() { SourceTagId = dose.Id, Factor = 1 }]
        }, _userId));
        Assert.Contains("'Effect later'", refusal.Message);

        // The backup import skips such a rule with a warning.
        var backups = new BackupService(_context, NullLogger<BackupService>.Instance, new TagRuleEngine(_context));
        var result = await backups.ImportDataAsync(new BackupData
        {
            Metadata = new BackupMetadata(),
            TagRules = [new TagRuleBackup { Name = "Effect total", TargetTagName = "Effect", Sources = [new() { SourceTagName = "Dose", Factor = 1 }] }]
        }, clearExistingData: false, _userId);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(1, result.Statistics.TagRulesSkipped);
        Assert.Contains(result.Warnings, w => w.Contains("'Effect total'") && w.Contains("trigger"));
        Assert.False(await _context.TagRules.AnyAsync());
        Assert.False((await _context.Tags.SingleAsync(t => t.Id == effect.Id)).IsComputed);
    }

    [Fact]
    public async Task ATriggerWhosePromptedTagIsComputed_SetsNoReminder()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.Integer);
        var trigger = await Trigger(dose, effect);

        // Data saved before rule validation refused this order.
        effect.IsComputed = true;
        await _context.SaveChangesAsync();

        await Log(dose, Minute(Now.AddMinutes(-10)), "100");

        Assert.Empty(await RemindersOf(trigger));
        Assert.True(await _context.EventLogs.AnyAsync(e => e.Message.Contains("set no reminder") && e.Message.Contains("computed")));
    }

    [Fact]
    public async Task TagsATriggerUses_CannotBeDeleted_AndDeletingTheTrigger_TakesItsOpenReminders()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var type = await _types.Create(new RecordTypeRequest { Name = "Run", Fields = [new() { Name = "Distance", TypeId = InputTypeIds.Decimal }] }, _userId);
        var recordType = await _context.Tags.SingleAsync(t => t.Id == type.Id);
        var trigger = await Trigger(dose, effect, name: "Effect later");
        var recordTrigger = await Trigger(dose, recordType, name: "Run later");
        var tags = new TagService(_context, new TagRepository(_context), NullLogger<TagService>.Instance);

        Assert.Contains("'Effect later'", (await Assert.ThrowsAsync<InvalidOperationException>(() => tags.Delete(effect.Id, _userId))).Message);
        Assert.Contains("'Run later'", (await Assert.ThrowsAsync<InvalidOperationException>(() => _types.Delete(type.Id, _userId))).Message);

        // One done reminder from yesterday and today's open one.
        await Log(dose, Minute(Now.AddMinutes(-5)), "100");
        var done = new Reminder
        {
            UserId = _userId,
            Title = "Effect later",
            RecurrenceType = RecurrenceType.Daily,
            TriggerId = trigger.Id,
            TriggerDay = DateOnly.FromDateTime(Now).AddDays(-1),
            MonitorFromDate = DateOnly.FromDateTime(Now).AddDays(-1),
            MonitorToDate = DateOnly.FromDateTime(Now).AddDays(-1)
        };
        done.Days.Add(new ReminderDay { UserId = _userId, Date = done.MonitorFromDate.Value, IsDone = true });
        _context.Reminders.Add(done);
        await _context.SaveChangesAsync();

        await _triggers.Delete(trigger.Id, _userId);
        _context.ChangeTracker.Clear();
        var left = await _context.Reminders.Where(r => r.Title == "Effect later").ToListAsync();
        var kept = Assert.Single(left);
        Assert.Equal((done.Id, (int?)null, (DateOnly?)null), (kept.Id, kept.TriggerId, kept.TriggerDay));
        Assert.Single(await RemindersOf(recordTrigger));
    }

    [Fact]
    public async Task AReminderLongPast_IsRemovedWhenTheTriggerFiresAgain()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        var trigger = await Trigger(dose, effect);
        var longAgo = DateOnly.FromDateTime(Now).AddDays(-TagTriggerEngine.KeepDays - 5);
        _context.Reminders.Add(new Reminder
        {
            UserId = _userId,
            Title = "Old",
            TriggerId = trigger.Id,
            TriggerDay = longAgo,
            MonitorFromDate = longAgo,
            MonitorToDate = longAgo
        });
        await _context.SaveChangesAsync();

        await Log(dose, Minute(Now.AddMinutes(-5)), "100");

        Assert.DoesNotContain(await RemindersOf(trigger), r => r.Title == "Old");
        Assert.Single(await RemindersOf(trigger));
    }

    [Fact]
    public async Task BackupRoundTrip_KeepsTheTriggers_ButNotTheirReminders()
    {
        var dose = await AddTag("Dose", InputTypeIds.Integer);
        var effect = await AddTag("Effect", InputTypeIds.String);
        await Trigger(dose, effect, TagRuleOperator.GreaterOrEqual, "50", name: "Effect later");
        await Log(dose, Minute(Now.AddMinutes(-5)), "100");
        var backups = new BackupService(_context, NullLogger<BackupService>.Instance, new TagRuleEngine(_context));

        var export = await backups.ExportDataAsync(_userId);
        var exported = Assert.Single(export.TagTriggers);
        Assert.Equal(("Dose", "Effect", "50", 180), (exported.IfTagName, exported.PromptTagName, exported.IfOperand, exported.OffsetMinutes));
        Assert.Equal(1, export.Metadata.TotalTagTriggers);
        Assert.Empty(export.Reminders);

        // Clearing removes the trigger and its open reminder.
        await backups.ClearDataAsync(_userId);
        Assert.False(await _context.TagTriggers.AnyAsync());
        Assert.False(await _context.Reminders.AnyAsync(r => r.TriggerId != null));

        await AddTag("Dose", InputTypeIds.Integer);
        await AddTag("Effect", InputTypeIds.String);
        var imported = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.Equal(1, imported.Statistics.TagTriggersImported);
        Assert.Equal(("Effect later", "GreaterOrEqual"), ((await _triggers.GetAll(_userId)).Single().Name, (await _triggers.GetAll(_userId)).Single().IfOperator));

        // A trigger whose tag is missing is skipped with a warning; one already there quietly.
        export.TagTriggers.Add(new TagTriggerBackup { Name = "Lost", IfTagName = "Missing", PromptTagName = "Effect", OffsetMinutes = 60 });
        var again = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.Equal((0, 2), (again.Statistics.TagTriggersImported, again.Statistics.TagTriggersSkipped));
        Assert.Contains(again.Warnings, w => w.Contains("'Lost'") && w.Contains("'Missing'"));
    }
}
