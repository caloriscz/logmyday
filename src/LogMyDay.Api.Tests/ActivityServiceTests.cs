using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

public class ActivityServiceTests
{
    [Fact]
    public async Task Create_ShouldAddActivityAndReturnResponse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: "ActivityService_Create_Test")
            .Options;
        using var context = new LogMyDayDbContext(options);
        var userId = Guid.NewGuid();
        // Add a tag to reference (since Activity requires TagId)
        var tag = new Tag { TagName = "TestTag", InputTypeId = 1, IsRequired = false, UserId = userId };
        context.Tags.Add(tag);
        context.SaveChanges();
        
        var repository = new ActivityRepository(context);
        var eventLogService = new EventLogService(context, NullLogger<EventLogService>.Instance);
        var tagDayLockService = new TagDayLockService(context);
        var service = new ActivityService(context, repository, eventLogService, tagDayLockService);
        var request = new ActivityRequest
        {
            DateStarted = DateTime.UtcNow.AddHours(-1),
            DateFinished = DateTime.UtcNow,
            Description = "Test Activity",
            PrimaryTagId = tag.Id
        };
        // Act
        var response = await service.Create(request, userId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.Description, response.Description);
        Assert.Equal(request.PrimaryTagId, response.PrimaryTagId);
        Assert.True(response.Id > 0);
        var activity = await context.Activities.FindAsync(response.Id);
        Assert.NotNull(activity);
        Assert.Equal(request.Description, activity.Description);
    }

    [Fact]
    public async Task Create_NonRepeatableNumeric_SecondCreate_AddsRequestValueNotStep()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Create_NonRepeatableNumeric_SecondCreate_AddsRequestValueNotStep))
            .Options;
        using var context = new LogMyDayDbContext(options);

        var userId = Guid.NewGuid();
        // Non-repeatable numeric tag with a Step that differs from the carried value, so a
        // regression (adding Step instead of the value) is unambiguous: Step 10 vs value 20.
        var tag = new Tag
        {
            TagName = "Zinc",
            InputTypeId = 1, // Integer
            IsRepeatable = false,
            TimeGranularity = TimeGranularity.Daily,
            Step = 10,
            IsRequired = false,
            UserId = userId
        };
        context.Tags.Add(tag);
        context.SaveChanges();

        var repository = new ActivityRepository(context);
        var eventLogService = new EventLogService(context, NullLogger<EventLogService>.Instance);
        var tagDayLockService = new TagDayLockService(context);
        var service = new ActivityService(context, repository, eventLogService, tagDayLockService);
        var when = DateTime.UtcNow;

        // Two completions, each "Add 20".
        await service.Create(new ActivityRequest { DateStarted = when, Description = "20", PrimaryTagId = tag.Id }, userId);
        await service.Create(new ActivityRequest { DateStarted = when, Description = "20", PrimaryTagId = tag.Id }, userId);

        var rows = await context.Activities.Where(a => a.TagId == tag.Id && a.UserId == userId).ToListAsync();
        var single = Assert.Single(rows);
        Assert.Equal("40", single.Description);
    }

    [Fact]
    public async Task Create_NonRepeatableNumeric_SecondCreate_NoValue_FallsBackToStep()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Create_NonRepeatableNumeric_SecondCreate_NoValue_FallsBackToStep))
            .Options;
        using var context = new LogMyDayDbContext(options);

        var userId = Guid.NewGuid();
        var tag = new Tag
        {
            TagName = "Quick",
            InputTypeId = 1,
            IsRepeatable = false,
            TimeGranularity = TimeGranularity.Daily,
            Step = 10,
            IsRequired = false,
            UserId = userId
        };
        context.Tags.Add(tag);
        context.SaveChanges();

        var repository = new ActivityRepository(context);
        var eventLogService = new EventLogService(context, NullLogger<EventLogService>.Instance);
        var tagDayLockService = new TagDayLockService(context);
        var service = new ActivityService(context, repository, eventLogService, tagDayLockService);
        var when = DateTime.UtcNow;

        await service.Create(new ActivityRequest { DateStarted = when, Description = "5", PrimaryTagId = tag.Id }, userId);
        await service.Create(new ActivityRequest { DateStarted = when, Description = null, PrimaryTagId = tag.Id }, userId);

        var single = Assert.Single(await context.Activities.Where(a => a.TagId == tag.Id && a.UserId == userId).ToListAsync());
        Assert.Equal("15", single.Description); // 5 + Step(10)
    }

    private static (ActivityService service, LogMyDayDbContext context) CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var context = new LogMyDayDbContext(options);
        var service = new ActivityService(
            context,
            new ActivityRepository(context),
            new EventLogService(context, NullLogger<EventLogService>.Instance),
            new TagDayLockService(context));

        return (service, context);
    }

    private static async Task<Tag> AddTag(LogMyDayDbContext context, Guid? ownerId)
    {
        var tag = new Tag { TagName = "Coffee", InputTypeId = 1, IsRequired = false, IsRepeatable = true, UserId = ownerId };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        return tag;
    }

    [Fact]
    public async Task Create_WithAnotherUsersTag_ThrowsNotFoundAndWritesNothing()
    {
        var (service, context) = CreateService(nameof(Create_WithAnotherUsersTag_ThrowsNotFoundAndWritesNothing));
        var foreignTag = await AddTag(context, Guid.NewGuid());
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.Create(new ActivityRequest { DateStarted = DateTime.Now, Description = "1", PrimaryTagId = foreignTag.Id }, userId));
        Assert.False(await context.Activities.AnyAsync());
    }

    [Fact]
    public async Task Create_WithUnownedTag_ThrowsNotFound()
    {
        var (service, context) = CreateService(nameof(Create_WithUnownedTag_ThrowsNotFound));
        var unownedTag = await AddTag(context, ownerId: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.Create(new ActivityRequest { DateStarted = DateTime.Now, Description = "1", PrimaryTagId = unownedTag.Id }, Guid.NewGuid()));
    }

    [Fact]
    public async Task Update_MovingActivityToAnotherUsersTag_ThrowsNotFoundAndKeepsTag()
    {
        var (service, context) = CreateService(nameof(Update_MovingActivityToAnotherUsersTag_ThrowsNotFoundAndKeepsTag));
        var userId = Guid.NewGuid();
        var ownTag = await AddTag(context, userId);
        var foreignTag = await AddTag(context, Guid.NewGuid());
        var created = await service.Create(new ActivityRequest { DateStarted = DateTime.Now, Description = "1", PrimaryTagId = ownTag.Id }, userId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.Update(created.Id, new ActivityRequest { DateStarted = DateTime.Now, Description = "2", PrimaryTagId = foreignTag.Id }, userId));

        context.ChangeTracker.Clear();
        var stored = await context.Activities.SingleAsync();
        Assert.Equal(ownTag.Id, stored.TagId);
        Assert.Equal("1", stored.Description);
    }

    [Fact]
    public async Task GetPeriodSum_WithAnotherUsersTag_ThrowsNotFound()
    {
        var (service, context) = CreateService(nameof(GetPeriodSum_WithAnotherUsersTag_ThrowsNotFound));
        var foreignTag = await AddTag(context, Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPeriodSum(foreignTag.Id, DateTime.Now, Guid.NewGuid()));
    }

    [Fact]
    public async Task TagDayLock_Upsert_OnAnotherUsersTag_ThrowsNotFound()
    {
        var (_, context) = CreateService(nameof(TagDayLock_Upsert_OnAnotherUsersTag_ThrowsNotFound));
        var foreignTag = await AddTag(context, Guid.NewGuid());
        var locks = new TagDayLockService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => locks.Upsert(
            Guid.NewGuid(),
            new TagDayLockRequest { TagId = foreignTag.Id, Date = DateOnly.FromDateTime(DateTime.Today), IsLocked = true },
            DayLockSetBy.User));
        Assert.False(await context.TagDayLocks.AnyAsync());
    }

    private static (ActivityService service, LogMyDayDbContext context, Guid userId, Tag tag) CreateServiceForPragueUser(string dbName)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var context = new LogMyDayDbContext(options);
        var userId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "t@t", PasswordHash = "x", TimeZone = "Europe/Prague", Culture = "en-US" });
        var tag = new Tag { TagName = "Coffee", InputTypeId = 1, IsRequired = false, UserId = userId };
        context.Tags.Add(tag);
        context.SaveChanges();

        var service = new ActivityService(
            context,
            new ActivityRepository(context),
            new EventLogService(context, NullLogger<EventLogService>.Instance),
            new TagDayLockService(context));

        return (service, context, userId, tag);
    }

    [Fact]
    public async Task Create_UtcTime_IsStoredAsUserLocalTime()
    {
        var (service, context, userId, tag) = CreateServiceForPragueUser(nameof(Create_UtcTime_IsStoredAsUserLocalTime));

        var response = await service.Create(new ActivityRequest
        {
            DateStarted = new DateTime(2026, 7, 1, 22, 30, 0, DateTimeKind.Utc),
            Description = "1",
            PrimaryTagId = tag.Id
        }, userId);

        var stored = await context.Activities.SingleAsync(a => a.Id == response.Id);
        Assert.Equal(new DateTime(2026, 7, 2, 0, 30, 0), stored.DateStarted);
        Assert.Equal(DateTimeKind.Unspecified, stored.DateStarted.Kind);
    }

    [Fact]
    public async Task Create_NaiveLocalTime_IsStoredUnchanged()
    {
        var (service, context, userId, tag) = CreateServiceForPragueUser(nameof(Create_NaiveLocalTime_IsStoredUnchanged));
        var local = new DateTime(2026, 7, 1, 23, 30, 0);

        var response = await service.Create(new ActivityRequest { DateStarted = local, Description = "1", PrimaryTagId = tag.Id }, userId);

        var stored = await context.Activities.SingleAsync(a => a.Id == response.Id);
        Assert.Equal(local, stored.DateStarted);
    }

    [Fact]
    public async Task Create_LateEveningLocalTime_ChecksTheLockOfThatLocalDay()
    {
        var (service, context, userId, tag) = CreateServiceForPragueUser(nameof(Create_LateEveningLocalTime_ChecksTheLockOfThatLocalDay));
        context.TagDayLocks.Add(new TagDayLock { UserId = userId, TagId = tag.Id, Date = new DateOnly(2026, 7, 1), IsLocked = true, SetAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // 23:30 local on the locked day. Read as UTC it would fall on 2 July in Prague and slip past the lock.
        await Assert.ThrowsAsync<TagDayLockedException>(() => service.Create(new ActivityRequest
        {
            DateStarted = new DateTime(2026, 7, 1, 23, 30, 0),
            Description = "1",
            PrimaryTagId = tag.Id
        }, userId));
    }

    private static async Task<Tag> AddNumericTag(LogMyDayDbContext context, Guid ownerId, int inputTypeId = 1, bool repeatable = true,
        TimeGranularity granularity = TimeGranularity.Daily, double? min = null, double? max = null)
    {
        var tag = new Tag
        {
            TagName = "Dose",
            InputTypeId = inputTypeId,
            IsRequired = false,
            IsRepeatable = repeatable,
            TimeGranularity = granularity,
            MinValue = min,
            MaxValue = max,
            UserId = ownerId
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        return tag;
    }

    private static async Task<Activity> AddRow(LogMyDayDbContext context, Guid userId, Tag tag, DateTime when, string value)
    {
        var row = new Activity { UserId = userId, TagId = tag.Id, DateStarted = when, DateCreated = DateTime.UtcNow, Description = value };
        context.Activities.Add(row);
        await context.SaveChangesAsync();

        return row;
    }

    [Fact]
    public async Task Update_ValueAboveMax_Throws()
    {
        var (service, context) = CreateService(nameof(Update_ValueAboveMax_Throws));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId, max: 10);
        var row = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 9, 0, 0), "5");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Update(row.Id, new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = row.DateStarted, Description = "11" }, userId));
    }

    [Fact]
    public async Task Update_RepeatablePeriodTotal_IsCheckedAgainstMax()
    {
        var (service, context) = CreateService(nameof(Update_RepeatablePeriodTotal_IsCheckedAgainstMax));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId, max: 10);
        var morning = new DateTime(2026, 7, 1, 9, 0, 0);
        await AddRow(context, userId, tag, morning, "6");
        var edited = await AddRow(context, userId, tag, morning.AddHours(5), "3");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Update(edited.Id, new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = edited.DateStarted, Description = "5" }, userId));

        var ok = await service.Update(edited.Id, new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = edited.DateStarted, Description = "4" }, userId);
        Assert.Equal("4", ok.Description);
    }

    [Fact]
    public async Task Update_And_Delete_OnLockedDay_AreAllowed()
    {
        var (service, context) = CreateService(nameof(Update_And_Delete_OnLockedDay_AreAllowed));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId);
        var row = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 9, 0, 0), "1");
        context.TagDayLocks.Add(new TagDayLock { UserId = userId, TagId = tag.Id, Date = new DateOnly(2026, 7, 1), IsLocked = true, SetAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var updated = await service.Update(row.Id, new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = row.DateStarted, Description = "2" }, userId);

        Assert.Equal("2", updated.Description);
        Assert.True(await service.Delete(row.Id, userId));
    }

    [Fact]
    public async Task Update_And_Delete_WriteEventLog()
    {
        var (service, context) = CreateService(nameof(Update_And_Delete_WriteEventLog));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId);
        var row = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 9, 0, 0), "1");

        await service.Update(row.Id, new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = row.DateStarted, Description = "2" }, userId);
        await service.Delete(row.Id, userId);

        var messages = await context.EventLogs.Where(e => e.UserId == userId).Select(e => e.Message).ToListAsync();
        Assert.Contains(messages, m => m.Contains("updated to value 2"));
        Assert.Contains(messages, m => m.Contains("deleted"));
    }

    [Fact]
    public async Task Create_BelowMin_Throws_ButSkipMarkerIsExempt()
    {
        var (service, context) = CreateService(nameof(Create_BelowMin_Throws_ButSkipMarkerIsExempt));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId, min: 1);
        var when = new DateTime(2026, 7, 1, 12, 0, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Create(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = when, Description = "0" }, userId));

        var skip = await service.Create(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = when, Description = "0" }, userId, isSkipMarker: true);
        Assert.Equal("0", skip.Description);
    }

    [Fact]
    public async Task Create_Accumulate_ReadsDecimalCommaAndWritesInvariant()
    {
        var (service, context) = CreateService(nameof(Create_Accumulate_ReadsDecimalCommaAndWritesInvariant));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId, inputTypeId: 6, repeatable: false);
        var when = new DateTime(2026, 7, 1, 9, 0, 0);
        await AddRow(context, userId, tag, when, "1,5");

        var result = await service.Create(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = when.AddHours(1), Description = "2" }, userId);

        Assert.Equal("3.5", result.Description);
    }

    [Fact]
    public async Task Create_Accumulate_RoundsIntegerAndUsesEarliestRow()
    {
        var (service, context) = CreateService(nameof(Create_Accumulate_RoundsIntegerAndUsesEarliestRow));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId, repeatable: false);
        var later = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 15, 0, 0), "10");
        var earliest = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 8, 0, 0), "2");

        await service.Create(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = new DateTime(2026, 7, 1, 18, 0, 0), Description = "1.6" }, userId);

        context.ChangeTracker.Clear();
        Assert.Equal("4", (await context.Activities.FindAsync(earliest.Id))!.Description); // 2 + 1.6 = 3.6, rounded
        Assert.Equal("10", (await context.Activities.FindAsync(later.Id))!.Description);
    }

    [Fact]
    public async Task ReplaceForDay_ReplacesEarliestRowOfThatDay_OrCreates()
    {
        var (service, context) = CreateService(nameof(ReplaceForDay_ReplacesEarliestRowOfThatDay_OrCreates));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId);
        var first = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 8, 0, 0), "1");
        var second = await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 20, 0, 0), "2");

        await service.ReplaceForDay(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = new DateTime(2026, 7, 1, 12, 0, 0), Description = "7" }, userId);
        await service.ReplaceForDay(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = new DateTime(2026, 7, 2, 12, 0, 0), Description = "3" }, userId);

        context.ChangeTracker.Clear();
        var rows = await context.Activities.Where(a => a.TagId == tag.Id).OrderBy(a => a.DateStarted).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(first.Id, rows[0].Id);
        Assert.Equal("7", rows[0].Description);
        Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 0), rows[0].DateStarted);
        Assert.Equal(second.Id, rows[1].Id);
        Assert.Equal("2", rows[1].Description);
        Assert.Equal("3", rows[2].Description);
    }

    [Fact]
    public async Task ReplaceForDay_EnforcesMax()
    {
        var (service, context) = CreateService(nameof(ReplaceForDay_EnforcesMax));
        var userId = Guid.NewGuid();
        var tag = await AddNumericTag(context, userId, max: 5);
        await AddRow(context, userId, tag, new DateTime(2026, 7, 1, 8, 0, 0), "1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReplaceForDay(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = new DateTime(2026, 7, 1, 12, 0, 0), Description = "9" }, userId));
    }
}
