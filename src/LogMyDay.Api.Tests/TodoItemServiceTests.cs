using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LogMyDay.Api.Tests;

public class TodoItemServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LogMyDayDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static (LogMyDayDbContext Context, Guid UserId, int ItemId, Tag Tag) SetupItemWithTag(
        string dbName,
        AutoLogMode autoLogMode = AutoLogMode.Add,
        string? itemNotes = null,
        int? monitorDaysBack = 7)
    {
        var context = CreateContext(dbName);
        var userId = Guid.NewGuid();

        var user = new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "hash" };
        context.Users.Add(user);

        var tag = new Tag { TagName = "Score", InputTypeId = 1, IsRequired = false };
        context.Tags.Add(tag);

        var list = new TodoList { UserId = userId, Name = "Reminders", ListType = TodoListType.Reminder };
        context.TodoLists.Add(list);
        context.SaveChanges();

        var item = new TodoItem
        {
            ListId = list.Id,
            Title = "Daily Score",
            Notes = itemNotes,
            CompletionTagId = tag.Id,
            AutoLogMode = autoLogMode,
            MonitorDaysBack = monitorDaysBack
        };
        context.TodoItems.Add(item);
        context.SaveChanges();

        return (context, userId, item.Id, tag);
    }

    private static (LogMyDayDbContext Context, Guid UserId, int ItemId) SetupItemWithoutTag(string dbName)
    {
        var context = CreateContext(dbName);
        var userId = Guid.NewGuid();

        var user = new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "hash" };
        context.Users.Add(user);

        var list = new TodoList { UserId = userId, Name = "Reminders", ListType = TodoListType.Reminder };
        context.TodoLists.Add(list);
        context.SaveChanges();

        var item = new TodoItem { ListId = list.Id, Title = "Simple Task" };
        context.TodoItems.Add(item);
        context.SaveChanges();

        return (context, userId, item.Id);
    }

    // ── No completion tag ─────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_NoCompletionTag_MarksDone_DoesNotLogActivity()
    {
        var (context, userId, itemId) = SetupItemWithoutTag(nameof(Complete_NoCompletionTag_MarksDone_DoesNotLogActivity));
        var activitySvc = new Mock<IActivityService>(MockBehavior.Strict);
        var svc = new TodoItemService(context, activitySvc.Object, NullLogger<TodoItemService>.Instance);

        var doneAt = DateTime.UtcNow;
        var result = await svc.Complete(itemId, new TodoItemCompleteRequest { DoneAt = doneAt }, userId);

        Assert.True(result.IsDone);
        Assert.Equal(doneAt, result.DoneAt);
        // Strict mock — any Create call would throw; verifying no activity was logged
        activitySvc.VerifyNoOtherCalls();
    }

    // ── With completion tag, AutoLogMode.Add ──────────────────────────────────

    [Fact]
    public async Task Complete_WithTag_CompletionValue_LogsActivityWithValue()
    {
        var (context, userId, itemId, tag) = SetupItemWithTag(
            nameof(Complete_WithTag_CompletionValue_LogsActivityWithValue));

        ActivityRequest? captured = null;
        var activitySvc = new Mock<IActivityService>();
        activitySvc
            .Setup(s => s.Create(It.IsAny<ActivityRequest>(), userId))
            .Callback<ActivityRequest, Guid>((req, _) => captured = req)
            .ReturnsAsync(new ActivityResponse { Id = 1, PrimaryTagName = tag.TagName, PrimaryTagValue = "" });

        var svc = new TodoItemService(context, activitySvc.Object, NullLogger<TodoItemService>.Instance);
        var doneAt = DateTime.UtcNow;

        var result = await svc.Complete(itemId, new TodoItemCompleteRequest { DoneAt = doneAt, CompletionValue = "42" }, userId);

        Assert.True(result.IsDone);
        activitySvc.Verify(s => s.Create(It.IsAny<ActivityRequest>(), userId), Times.Once);
        Assert.Equal("42", captured?.Description);
        Assert.Equal(doneAt, captured?.DateStarted);
    }

    [Fact]
    public async Task Complete_WithTag_NullValue_ItemNotesSet_LogsActivityWithItemNotes()
    {
        var (context, userId, itemId, tag) = SetupItemWithTag(
            nameof(Complete_WithTag_NullValue_ItemNotesSet_LogsActivityWithItemNotes),
            itemNotes: "default-value");

        ActivityRequest? captured = null;
        var activitySvc = new Mock<IActivityService>();
        activitySvc
            .Setup(s => s.Create(It.IsAny<ActivityRequest>(), userId))
            .Callback<ActivityRequest, Guid>((req, _) => captured = req)
            .ReturnsAsync(new ActivityResponse { Id = 1, PrimaryTagName = tag.TagName, PrimaryTagValue = "" });

        var svc = new TodoItemService(context, activitySvc.Object, NullLogger<TodoItemService>.Instance);

        await svc.Complete(itemId, new TodoItemCompleteRequest { DoneAt = DateTime.UtcNow, CompletionValue = null }, userId);

        activitySvc.Verify(s => s.Create(It.IsAny<ActivityRequest>(), userId), Times.Once);
        Assert.Equal("default-value", captured?.Description);
    }

    [Fact]
    public async Task Complete_WithTag_BothNull_LogsActivityWithNullDescription()
    {
        // Documents current behaviour: activity IS created even with null description.
        // After AllowUnfilled server-side guard this would change for numeric tags.
        var (context, userId, itemId, tag) = SetupItemWithTag(
            nameof(Complete_WithTag_BothNull_LogsActivityWithNullDescription),
            itemNotes: null);

        ActivityRequest? captured = null;
        var activitySvc = new Mock<IActivityService>();
        activitySvc
            .Setup(s => s.Create(It.IsAny<ActivityRequest>(), userId))
            .Callback<ActivityRequest, Guid>((req, _) => captured = req)
            .ReturnsAsync(new ActivityResponse { Id = 1, PrimaryTagName = tag.TagName, PrimaryTagValue = "" });

        var svc = new TodoItemService(context, activitySvc.Object, NullLogger<TodoItemService>.Instance);

        await svc.Complete(itemId, new TodoItemCompleteRequest { DoneAt = DateTime.UtcNow, CompletionValue = null }, userId);

        activitySvc.Verify(s => s.Create(It.IsAny<ActivityRequest>(), userId), Times.Once);
        Assert.Null(captured?.Description);
    }

    // ── AutoLogMode.ResetIfExists ─────────────────────────────────────────────

    [Fact]
    public async Task Complete_ResetIfExists_ExistingActivity_UpdatesNotDuplicates()
    {
        var (context, userId, itemId, tag) = SetupItemWithTag(
            nameof(Complete_ResetIfExists_ExistingActivity_UpdatesNotDuplicates),
            autoLogMode: AutoLogMode.ResetIfExists);

        // Seed an existing activity within the monitoring window (MonitorDaysBack=7)
        var existing = new Activity
        {
            TagId = tag.Id,
            UserId = userId,
            DateStarted = DateTime.UtcNow.AddDays(-1), // within 7-day window
            DateCreated = DateTime.UtcNow.AddDays(-1),
            Description = "old-value"
        };
        context.Activities.Add(existing);
        context.SaveChanges();

        var activitySvc = new Mock<IActivityService>();
        // Create should NOT be called — existing activity is updated in-place
        var svc = new TodoItemService(context, activitySvc.Object, NullLogger<TodoItemService>.Instance);

        var doneAt = DateTime.UtcNow;
        var result = await svc.Complete(itemId, new TodoItemCompleteRequest { DoneAt = doneAt, CompletionValue = "new-value" }, userId);

        Assert.True(result.IsDone);
        activitySvc.Verify(s => s.Create(It.IsAny<ActivityRequest>(), It.IsAny<Guid>()), Times.Never);

        // Verify the existing activity was updated
        var updated = context.Activities.Find(existing.Id);
        Assert.Equal("new-value", updated?.Description);
        Assert.Equal(doneAt, updated?.DateStarted);
    }

    [Fact]
    public async Task Complete_ResetIfExists_NoExistingActivity_CreatesNew()
    {
        var (context, userId, itemId, tag) = SetupItemWithTag(
            nameof(Complete_ResetIfExists_NoExistingActivity_CreatesNew),
            autoLogMode: AutoLogMode.ResetIfExists);

        var activitySvc = new Mock<IActivityService>();
        activitySvc
            .Setup(s => s.Create(It.IsAny<ActivityRequest>(), userId))
            .ReturnsAsync(new ActivityResponse { Id = 99, PrimaryTagName = tag.TagName, PrimaryTagValue = "" });

        var svc = new TodoItemService(context, activitySvc.Object, NullLogger<TodoItemService>.Instance);

        var result = await svc.Complete(itemId, new TodoItemCompleteRequest { DoneAt = DateTime.UtcNow, CompletionValue = "first-time" }, userId);

        Assert.True(result.IsDone);
        activitySvc.Verify(s => s.Create(It.IsAny<ActivityRequest>(), userId), Times.Once);
    }
}
