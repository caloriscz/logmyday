using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

public class TodoItemServiceTests
{
    private static (TodoItemService service, LogMyDayDbContext context, Guid userId) CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var context = new LogMyDayDbContext(options);

        var userId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "t@t", PasswordHash = "x", TimeZone = "UTC", Culture = "en-US" });
        context.SaveChanges();

        var activityService = new ActivityService(
            context,
            new ActivityRepository(context),
            new EventLogService(context, NullLogger<EventLogService>.Instance),
            new TagDayLockService(context));

        var service = new TodoItemService(
            context,
            activityService,
            new EventLogService(context, NullLogger<EventLogService>.Instance),
            NullLogger<TodoItemService>.Instance);

        return (service, context, userId);
    }

    private static async Task<(int listId, int itemId)> AddListWithItem(LogMyDayDbContext context, Guid userId, int? completionTagId)
    {
        var list = new TodoList { UserId = userId, Name = "Shopping", CompletionTagId = completionTagId, AutoLogMode = AutoLogMode.Add };
        context.TodoLists.Add(list);
        await context.SaveChangesAsync();

        var item = new TodoItem { ListId = list.Id, Title = "Milk" };
        context.TodoItems.Add(item);
        await context.SaveChangesAsync();

        return (list.Id, item.Id);
    }

    [Fact]
    public async Task Complete_LoggsActivityAgainstListTag()
    {
        var (service, context, userId) = CreateService(nameof(Complete_LoggsActivityAgainstListTag));
        var tag = new Tag { TagName = "Groceries", IsRequired = false };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var (_, itemId) = await AddListWithItem(context, userId, tag.Id);

        await service.Complete(itemId, new TodoItemCompleteRequest { DoneAt = DateTime.UtcNow }, userId);

        var activity = await context.Activities.SingleAsync(a => a.UserId == userId);
        Assert.Equal(tag.Id, activity.TagId);
        Assert.Equal("Milk", activity.Description);
    }

    [Fact]
    public async Task Complete_UntaggedList_LogsNoActivity()
    {
        var (service, context, userId) = CreateService(nameof(Complete_UntaggedList_LogsNoActivity));
        var (_, itemId) = await AddListWithItem(context, userId, completionTagId: null);

        await service.Complete(itemId, new TodoItemCompleteRequest { DoneAt = DateTime.UtcNow }, userId);

        Assert.False(await context.Activities.AnyAsync(a => a.UserId == userId));
        var item = await context.TodoItems.FindAsync(itemId);
        Assert.True(item!.IsDone);
    }

    [Fact]
    public async Task Create_IgnoresItemLevelCompletionTag()
    {
        var (service, context, userId) = CreateService(nameof(Create_IgnoresItemLevelCompletionTag));
        var tag = new Tag { TagName = "Groceries", IsRequired = false };
        context.Tags.Add(tag);
        var list = new TodoList { UserId = userId, Name = "Shopping" };
        context.TodoLists.Add(list);
        await context.SaveChangesAsync();

        var created = await service.Create(new TodoItemRequest
        {
            ListId = list.Id,
            Title = "Bread",
            CompletionTagId = tag.Id // must be ignored — tag lives on the list
        }, userId);

        var stored = await context.TodoItems.FindAsync(created.Id);
        Assert.Null(stored!.CompletionTagId);
    }
}
