using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using LogMyDay.UI.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.App.Tests;

public class RecordCompletionTimeTests
{
    [Fact]
    public void Today_TheRecordAndTheCompletionAreNow()
    {
        var before = DateTime.UtcNow;
        var record = RecordCompletionTime.RecordTime(DateTime.Today);
        var doneAt = RecordCompletionTime.DoneAt(DateTime.Today);
        var after = DateTime.UtcNow;

        Assert.InRange(record.ToUniversalTime(), before, after);
        Assert.Equal(DateTimeKind.Utc, doneAt.Kind);
        Assert.InRange(doneAt, before, after);
    }

    [Fact]
    public void AnotherDay_TheRecordAndTheCompletionAreItsNoon()
    {
        var day = DateTime.SpecifyKind(DateTime.Today.AddDays(-3), DateTimeKind.Unspecified);

        Assert.Equal(day.AddHours(12), RecordCompletionTime.RecordTime(day));
        Assert.Equal(DateTime.SpecifyKind(day.AddHours(12), DateTimeKind.Local).ToUniversalTime(), RecordCompletionTime.DoneAt(day));
    }

    // The server places the record (sent with the app's offset) and the completion (UTC) on the
    // user's days the same way, so one instant finds the record even for a user whose date differs
    // from the app's. That the two times are one instant is the test above; a midnight split between
    // moderate zones (a web server in UTC, a user in Prague) cannot be staged with the machine clock.
    [Fact]
    public async Task ARecordCompletion_Succeeds_WhenTheUsersDateDiffersFromTheApps()
    {
        var zoneId = new[] { "Pacific/Kiritimati", "Etc/GMT+12" }.First(id =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)))
                != DateOnly.FromDateTime(DateTime.Today));

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var context = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(connection).Options);
        context.Database.EnsureCreated();
        var userId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "t@t", PasswordHash = "x", TimeZone = zoneId, Culture = "en-US" });
        context.SaveChanges();

        var eventLog = new EventLogService(context, NullLogger<EventLogService>.Instance);
        var engine = new TagRuleEngine(context);
        var activities = new ActivityService(context, new ActivityRepository(context), eventLog, new TagDayLockService(context), engine);
        var types = new RecordTypeService(context, eventLog);
        var records = new RecordService(context, eventLog, engine);
        var reminders = new ReminderService(context, activities, eventLog, NullLogger<ReminderService>.Instance);

        var type = await types.Create(new RecordTypeRequest { Name = "Blood pressure", Fields = [new() { Name = "Systolic", TypeId = InputTypeIds.Integer }] }, userId);
        var reminder = await reminders.Create(new ReminderRequest { Title = "Measure blood pressure", CompletionTagId = type.Id }, userId);
        var record = await records.Create(new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = RecordCompletionTime.RecordTime(DateTime.Today),
            Values = [new RecordValueRequest { TagId = type.Fields[0].TagId, Value = "128" }]
        }, userId);
        context.ChangeTracker.Clear();

        var done = await reminders.Complete(reminder.Id, new ReminderCompleteRequest
        {
            DoneAt = RecordCompletionTime.DoneAt(DateTime.Today),
            RecordId = record.Id
        }, userId);

        Assert.True(done.IsDone);
    }
}
