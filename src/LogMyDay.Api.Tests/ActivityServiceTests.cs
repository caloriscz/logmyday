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
}
