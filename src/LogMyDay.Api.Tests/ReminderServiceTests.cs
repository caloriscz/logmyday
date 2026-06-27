using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

public class ReminderServiceTests
{
    private static (ReminderService service, LogMyDayDbContext context, Guid userId) CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var context = new LogMyDayDbContext(options);

        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = "t@t",
            PasswordHash = "x",
            TimeZone = "UTC",
            Culture = "en-US"
        });
        context.SaveChanges();

        var activityService = new ActivityService(
            context,
            new ActivityRepository(context),
            new EventLogService(context, NullLogger<EventLogService>.Instance),
            new TagDayLockService(context));

        var service = new ReminderService(
            context,
            activityService,
            new EventLogService(context, NullLogger<EventLogService>.Instance),
            NullLogger<ReminderService>.Instance);

        return (service, context, userId);
    }

    private static async Task<int> AddDailyReminder(LogMyDayDbContext context, Guid userId)
    {
        var reminder = new Reminder { UserId = userId, Title = "Pill", RecurrenceType = RecurrenceType.Daily };
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        return reminder.Id;
    }

    private static async Task<int> AddTag(LogMyDayDbContext context, Guid userId, int inputTypeId)
    {
        var tag = new Tag { TagName = "T", UserId = userId, InputTypeId = inputTypeId };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        return tag.Id;
    }

    private static async Task<int> AddDailyReminderWithTag(LogMyDayDbContext context, Guid userId, int tagId)
    {
        var reminder = new Reminder { UserId = userId, Title = "Pill", RecurrenceType = RecurrenceType.Daily, CompletionTagId = tagId };
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        return reminder.Id;
    }

    [Fact]
    public async Task Skip_TwoDifferentDays_BothRemainSkippedIndependently()
    {
        var (service, context, userId) = CreateService(nameof(Skip_TwoDifferentDays_BothRemainSkippedIndependently));
        var id = await AddDailyReminder(context, userId);

        var dayA = new DateOnly(2026, 6, 1);
        var dayB = new DateOnly(2026, 6, 3);

        await service.Skip(id, userId, dayA);
        await service.Skip(id, userId, dayB);

        var onA = (await service.GetAll(userId, dayA)).Single();
        var onB = (await service.GetAll(userId, dayB)).Single();
        var onBetween = (await service.GetAll(userId, new DateOnly(2026, 6, 2))).Single();

        Assert.True(onA.IsSkipped);
        Assert.True(onB.IsSkipped);
        Assert.False(onBetween.IsSkipped);
    }

    [Fact]
    public async Task Complete_Today_DoesNotMarkPastDayDone()
    {
        var (service, context, userId) = CreateService(nameof(Complete_Today_DoesNotMarkPastDayDone));
        var id = await AddDailyReminder(context, userId);

        var today = new DateOnly(2026, 6, 6);
        var yesterday = new DateOnly(2026, 6, 5);

        await service.Complete(id, new ReminderCompleteRequest
        {
            DoneAt = new DateTime(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc)
        }, userId);

        var onToday = (await service.GetAll(userId, today)).Single();
        var onYesterday = (await service.GetAll(userId, yesterday)).Single();

        Assert.True(onToday.IsDone);
        Assert.False(onYesterday.IsDone);
    }

    [Fact]
    public async Task SkipUnskipSkip_SameDay_EndsSkipped()
    {
        var (service, context, userId) = CreateService(nameof(SkipUnskipSkip_SameDay_EndsSkipped));
        var id = await AddDailyReminder(context, userId);
        var day = new DateOnly(2026, 6, 4);

        await service.Skip(id, userId, day);
        await service.Unskip(id, userId, day);
        Assert.False((await service.GetAll(userId, day)).Single().IsSkipped);

        await service.Skip(id, userId, day);
        Assert.True((await service.GetAll(userId, day)).Single().IsSkipped);
    }

    [Fact]
    public async Task Write_PrunesRowsOutsideMonitoringWindow_KeepsRecentAndCurrent()
    {
        var (service, context, userId) = CreateService(nameof(Write_PrunesRowsOutsideMonitoringWindow_KeepsRecentAndCurrent));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reminder = new Reminder { UserId = userId, Title = "Pill", RecurrenceType = RecurrenceType.Daily, MonitorFromDate = today.AddDays(-7) };
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        context.ReminderDays.AddRange(
            new ReminderDay { ReminderId = reminder.Id, UserId = userId, Date = today.AddDays(-100), IsSkipped = true },
            new ReminderDay { ReminderId = reminder.Id, UserId = userId, Date = today.AddDays(-3), IsSkipped = true });
        await context.SaveChangesAsync();

        // Any write action prunes this reminder's aged-out rows.
        await service.Skip(reminder.Id, userId, today);

        var remaining = context.ReminderDays
            .Where(d => d.ReminderId == reminder.Id)
            .Select(d => d.Date)
            .ToList();

        Assert.DoesNotContain(today.AddDays(-100), remaining); // before MonitorFromDate
        Assert.Contains(today.AddDays(-3), remaining);         // within window
        Assert.Contains(today, remaining);                     // current period kept
    }

    [Fact]
    public async Task Skip_NumericCompletionTag_LogsZeroActivity()
    {
        var (service, context, userId) = CreateService(nameof(Skip_NumericCompletionTag_LogsZeroActivity));
        var tagId = await AddTag(context, userId, 1); // Integer
        var id = await AddDailyReminderWithTag(context, userId, tagId);

        await service.Skip(id, userId, new DateOnly(2026, 6, 10));

        var activity = context.Activities.Single(a => a.TagId == tagId && a.UserId == userId);
        Assert.Equal("0", activity.Description);
    }

    [Fact]
    public async Task Skip_BooleanCompletionTag_LogsFalseActivity()
    {
        var (service, context, userId) = CreateService(nameof(Skip_BooleanCompletionTag_LogsFalseActivity));
        var tagId = await AddTag(context, userId, 3); // Boolean
        var id = await AddDailyReminderWithTag(context, userId, tagId);

        await service.Skip(id, userId, new DateOnly(2026, 6, 10));

        var activity = context.Activities.Single(a => a.TagId == tagId && a.UserId == userId);
        Assert.Equal("false", activity.Description);
    }

    [Fact]
    public async Task Skip_StringCompletionTag_LogsEmptyActivity()
    {
        var (service, context, userId) = CreateService(nameof(Skip_StringCompletionTag_LogsEmptyActivity));
        var tagId = await AddTag(context, userId, 2); // String
        var id = await AddDailyReminderWithTag(context, userId, tagId);

        await service.Skip(id, userId, new DateOnly(2026, 6, 10));

        var activity = context.Activities.Single(a => a.TagId == tagId && a.UserId == userId);
        Assert.Null(activity.Description);
    }

    [Fact]
    public async Task Skip_AddsValueEveryTime_NoDedup()
    {
        var (service, context, userId) = CreateService(nameof(Skip_AddsValueEveryTime_NoDedup));
        var tagId = await AddTag(context, userId, 1); // Integer
        var id = await AddDailyReminderWithTag(context, userId, tagId);

        var day = new DateOnly(2026, 6, 10);
        await service.Skip(id, userId, day);
        await service.Skip(id, userId, day);

        Assert.Equal(2, context.Activities.Count(a => a.TagId == tagId && a.UserId == userId));
    }

    [Fact]
    public async Task Skip_TaglessReminder_LogsNoActivity()
    {
        var (service, context, userId) = CreateService(nameof(Skip_TaglessReminder_LogsNoActivity));
        var id = await AddDailyReminder(context, userId);

        await service.Skip(id, userId, new DateOnly(2026, 6, 10));

        Assert.Empty(context.Activities);
    }

    [Fact]
    public async Task Create_NoneRecurrence_IsCoercedToDaily()
    {
        var (service, context, userId) = CreateService(nameof(Create_NoneRecurrence_IsCoercedToDaily));

        var created = await service.Create(new ReminderRequest
        {
            Title = "Pill",
            RecurrenceType = RecurrenceType.None
        }, userId);

        Assert.Equal(RecurrenceType.Daily, created.RecurrenceType);
        var stored = await context.Reminders.FindAsync(created.Id);
        Assert.Equal(RecurrenceType.Daily, stored!.RecurrenceType);
    }
}
