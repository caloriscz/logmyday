using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

public class TodoListServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LogMyDayDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static (Guid UserId, int ListId, int ItemId) SeedRecurringItem(
        LogMyDayDbContext context,
        RecurrenceType recurrence,
        bool isDone,
        DateTime? doneAt,
        string timeZone = "UTC",
        string culture = "en-US")
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = $"{userId}@test.com",
            PasswordHash = "hash",
            TimeZone = timeZone,
            Culture = culture
        };
        context.Users.Add(user);

        var list = new TodoList
        {
            UserId = userId,
            Name = "Test List",
            ListType = TodoListType.Reminder
        };
        context.TodoLists.Add(list);
        context.SaveChanges();

        var item = new TodoItem
        {
            ListId = list.Id,
            Title = "Test Item",
            RecurrenceType = recurrence,
            IsDone = isDone,
            DoneAt = doneAt
        };
        context.TodoItems.Add(item);
        context.SaveChanges();

        return (userId, list.Id, item.Id);
    }

    // ── Non-recurring ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NonRecurring_IsDone_True_ReturnsTrue_ForAnyDate()
    {
        using var context = CreateContext(nameof(GetAll_NonRecurring_IsDone_True_ReturnsTrue_ForAnyDate));
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.None, isDone: true, doneAt: DateTime.UtcNow.AddDays(-5));
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var result = await svc.GetAll(userId, yesterday);

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.True(item.IsDone);
    }

    [Fact]
    public async Task GetAll_NonRecurring_IsDone_False_ReturnsFalse_ForAnyDate()
    {
        using var context = CreateContext(nameof(GetAll_NonRecurring_IsDone_False_ReturnsFalse_ForAnyDate));
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.None, isDone: false, doneAt: null);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var result = await svc.GetAll(userId, DateOnly.FromDateTime(DateTime.UtcNow));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.False(item.IsDone);
    }

    // ── Daily recurring ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Daily_DoneYesterdayNoon_QueriedYesterday_ReturnsTrue()
    {
        using var context = CreateContext(nameof(GetAll_Daily_DoneYesterdayNoon_QueriedYesterday_ReturnsTrue));
        var yesterdayNoon = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Daily, isDone: true, doneAt: yesterdayNoon);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var result = await svc.GetAll(userId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.True(item.IsDone);
    }

    [Fact]
    public async Task GetAll_Daily_DoneYesterdayNoon_QueriedToday_ReturnsFalse()
    {
        using var context = CreateContext(nameof(GetAll_Daily_DoneYesterdayNoon_QueriedToday_ReturnsFalse));
        var yesterdayNoon = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Daily, isDone: true, doneAt: yesterdayNoon);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var result = await svc.GetAll(userId, DateOnly.FromDateTime(DateTime.UtcNow));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.False(item.IsDone);
    }

    [Fact]
    public async Task GetAll_Daily_DoneTodayNoon_QueriedToday_ReturnsTrue()
    {
        using var context = CreateContext(nameof(GetAll_Daily_DoneTodayNoon_QueriedToday_ReturnsTrue));
        var todayNoon = DateTime.UtcNow.Date.AddHours(12);
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Daily, isDone: true, doneAt: todayNoon);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var result = await svc.GetAll(userId, DateOnly.FromDateTime(DateTime.UtcNow));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.True(item.IsDone);
    }

    [Fact]
    public async Task GetAll_Daily_DoneTodayNoon_QueriedYesterday_ReturnsFalse()
    {
        using var context = CreateContext(nameof(GetAll_Daily_DoneTodayNoon_QueriedYesterday_ReturnsFalse));
        var todayNoon = DateTime.UtcNow.Date.AddHours(12);
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Daily, isDone: true, doneAt: todayNoon);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var result = await svc.GetAll(userId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.False(item.IsDone);
    }

    [Fact]
    public async Task GetAll_Daily_NotDone_ReturnsFalse_ForAnyDate()
    {
        using var context = CreateContext(nameof(GetAll_Daily_NotDone_ReturnsFalse_ForAnyDate));
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Daily, isDone: false, doneAt: null);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        var result = await svc.GetAll(userId, DateOnly.FromDateTime(DateTime.UtcNow));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.False(item.IsDone);
    }

    // ── Weekly recurring ──────────────────────────────────────────────────────

    // Uses fixed dates to avoid flakiness: week of 2025-01-06 (Mon) to 2025-01-12 (Sun).
    // en-US culture: first day = Sunday → week of 2025-01-05 (Sun) to 2025-01-11 (Sat).
    // We query 2025-01-06 (Mon) which is inside both Sunday-start and Monday-start weeks
    // containing 2025-01-08 (Wed, the done date), so the result is deterministic.

    [Fact]
    public async Task GetAll_Weekly_DoneWed_QueriedMonSameWeek_ReturnsTrue()
    {
        using var context = CreateContext(nameof(GetAll_Weekly_DoneWed_QueriedMonSameWeek_ReturnsTrue));
        // Wednesday 2025-01-08 noon UTC
        var doneAt = new DateTime(2025, 1, 8, 12, 0, 0, DateTimeKind.Utc);
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Weekly, isDone: true, doneAt: doneAt);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        // Monday 2025-01-06 — same week regardless of Sunday or Monday week-start
        var result = await svc.GetAll(userId, new DateOnly(2025, 1, 6));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.True(item.IsDone);
    }

    [Fact]
    public async Task GetAll_Weekly_DoneWed_QueriedMonNextWeek_ReturnsFalse()
    {
        using var context = CreateContext(nameof(GetAll_Weekly_DoneWed_QueriedMonNextWeek_ReturnsFalse));
        var doneAt = new DateTime(2025, 1, 8, 12, 0, 0, DateTimeKind.Utc);
        var (userId, _, itemId) = SeedRecurringItem(context, RecurrenceType.Weekly, isDone: true, doneAt: doneAt);
        var svc = new TodoListService(context, NullLogger<TodoListService>.Instance);

        // Monday 2025-01-13 — following week
        var result = await svc.GetAll(userId, new DateOnly(2025, 1, 13));

        var item = result.SelectMany(l => l.Items).Single(i => i.Id == itemId);
        Assert.False(item.IsDone);
    }
}
