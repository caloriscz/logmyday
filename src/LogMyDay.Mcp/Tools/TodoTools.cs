using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Todo lists hold items; completing an item can log the item's title to the list's completion
/// tag. Deleting a list takes its items with it, hence the sentinel.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TodoTools(
    McpUserContext user,
    ITodoListService lists,
    ITodoItemService items,
    ITagService tags,
    UserClock clock)
{
    public const string DeleteSentinelPrefix = "DELETE_LIST_";

    public sealed record ReorderItem([property: Description("Item id.")] int Id, [property: Description("New position; lower first.")] int DisplayOrder);

    // --- Lists ---

    [McpServerTool(Name = "list_todo_lists", Title = "List todo lists", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's todo lists with their items and each item's done/skipped state for a date (default today in the user's time zone).")]
    public async Task<IList<TodoListResponse>> ListTodoLists([Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        return await lists.GetAll(user.UserId, await ParseOrToday(date));
    }

    [McpServerTool(Name = "get_todo_list", Title = "Get todo list", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one todo list with its items.")]
    public Task<TodoListResponse> GetTodoList([Description("List id.")] int listId) => lists.GetById(listId, user.UserId);

    [McpServerTool(Name = "create_todo_list", Title = "Create todo list", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a todo list. With a completionTagId, completing an item logs the item's TITLE as the tag's value (so use a text tag); autoLogMode Add appends a row per completion, ResetIfExists reuses the day's row.")]
    public async Task<TodoListResponse> CreateTodoList(
        [Description("List name.")] string name,
        [Description("Tag that receives a row per completed item (must belong to the user).")] int? completionTagId = null,
        [Description("Add (default) or ResetIfExists.")] AutoLogMode autoLogMode = AutoLogMode.Add,
        [Description("Show the list on the home page.")] bool showOnHomepage = false,
        [Description("Sort position among lists.")] int displayOrder = 0)
    {
        if (completionTagId is int tagId)
        {
            await tags.GetTagById(tagId, user.UserId);
        }

        var request = new TodoListRequest
        {
            Name = RequireText(name, "name"),
            CompletionTagId = completionTagId,
            AutoLogMode = autoLogMode,
            ShowOnHomepage = showOnHomepage,
            DisplayOrder = displayOrder
        };

        return await lists.Create(request, user.UserId);
    }

    [McpServerTool(Name = "update_todo_list", Title = "Update todo list", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a todo list; omitted arguments keep their current value. Pass 0 for completionTagId to detach the tag.")]
    public async Task<TodoListResponse> UpdateTodoList(
        [Description("List id.")] int listId,
        string? name = null,
        [Description("Tag id, or 0 to detach.")] int? completionTagId = null,
        AutoLogMode? autoLogMode = null,
        bool? showOnHomepage = null,
        int? displayOrder = null)
    {
        var current = await lists.GetById(listId, user.UserId);
        var newTagId = completionTagId switch { null => current.CompletionTagId, 0 => null, _ => completionTagId };
        if (newTagId is int tagId && tagId != current.CompletionTagId)
        {
            await tags.GetTagById(tagId, user.UserId);
        }

        var request = new TodoListRequest
        {
            Name = name == null ? current.Name : RequireText(name, "name"),
            CompletionTagId = newTagId,
            AutoLogMode = autoLogMode ?? current.AutoLogMode,
            ShowOnHomepage = showOnHomepage ?? current.ShowOnHomepage,
            DisplayOrder = displayOrder ?? current.DisplayOrder
        };

        await lists.Update(listId, request, user.UserId);

        return await lists.GetById(listId, user.UserId);
    }

    [McpServerTool(Name = "delete_todo_list", Title = "Delete todo list", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a todo list AND all of its items. Requires confirm = \"DELETE_LIST_<listId>\"; without it nothing is deleted and the response reports how many items would go. Activities logged from the list are kept.")]
    public async Task<object> DeleteTodoList(
        [Description("List id.")] int listId,
        [Description("Must be exactly DELETE_LIST_<listId>.")] string? confirm = null)
    {
        var list = await lists.GetById(listId, user.UserId);
        ConfirmSentinel.Require(confirm, DeleteSentinelPrefix + listId,
            $"Deletes list {listId} '{list.Name}' and its {list.Items.Count} items.");

        await lists.Delete(listId, user.UserId);

        return new { deleted = true, listId, name = list.Name, itemsDeleted = list.Items.Count };
    }

    // --- Items ---

    [McpServerTool(Name = "create_todo_item", Title = "Create todo item", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Adds an item to a list. recurrenceType None (default) is a one-off; Daily/Weekly items reset each period. Dates are yyyy-MM-dd (a time may be added as yyyy-MM-ddTHH:mm).")]
    public async Task<TodoItemResponse> CreateTodoItem(
        [Description("List id.")] int listId,
        [Description("Item title; also the value logged to the list's completion tag.")] string title,
        string? notes = null,
        [Description("When the item becomes relevant.")] string? startDate = null,
        [Description("When it is due.")] string? dueDate = null,
        [Description("Time of day to notify, HH:mm.")] string? notifyAt = null,
        [Description("None (default), Daily or Weekly.")] RecurrenceType recurrenceType = RecurrenceType.None,
        [Description("Position in the list.")] int displayOrder = 0)
    {
        await lists.GetById(listId, user.UserId);
        var zone = await clock.Zone(user.UserId);

        var request = new TodoItemRequest
        {
            ListId = listId,
            Title = RequireText(title, "title"),
            Notes = notes,
            StartDate = startDate == null ? null : DateArguments.ParseDateTime(startDate, "startDate", zone),
            DueDate = dueDate == null ? null : DateArguments.ParseDateTime(dueDate, "dueDate", zone),
            NotifyAt = notifyAt == null ? null : DateArguments.ParseTime(notifyAt, "notifyAt"),
            RecurrenceType = recurrenceType,
            DisplayOrder = displayOrder
        };

        return await items.Create(request, user.UserId);
    }

    [McpServerTool(Name = "update_todo_item", Title = "Update todo item", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates an item; omitted arguments keep their current value. Pass \"\" for startDate, dueDate or notifyAt to clear them. listId moves the item to another list.")]
    public async Task<TodoItemResponse> UpdateTodoItem(
        [Description("Item id.")] int itemId,
        [Description("Move to this list.")] int? listId = null,
        string? title = null,
        string? notes = null,
        [Description("yyyy-MM-dd[THH:mm], or \"\" to clear.")] string? startDate = null,
        [Description("yyyy-MM-dd[THH:mm], or \"\" to clear.")] string? dueDate = null,
        [Description("HH:mm, or \"\" to clear.")] string? notifyAt = null,
        RecurrenceType? recurrenceType = null,
        int? displayOrder = null)
    {
        var (current, _) = await RequireItem(itemId);
        if (listId is int target && target != current.ListId)
        {
            await lists.GetById(target, user.UserId);
        }

        var zone = await clock.Zone(user.UserId);
        var request = new TodoItemRequest
        {
            ListId = listId ?? current.ListId,
            Title = title == null ? current.Title : RequireText(title, "title"),
            Notes = notes ?? current.Notes,
            StartDate = startDate switch { null => current.StartDate, "" => null, _ => DateArguments.ParseDateTime(startDate, "startDate", zone) },
            DueDate = dueDate switch { null => current.DueDate, "" => null, _ => DateArguments.ParseDateTime(dueDate, "dueDate", zone) },
            NotifyAt = notifyAt switch { null => current.NotifyAt, "" => null, _ => DateArguments.ParseTime(notifyAt, "notifyAt") },
            RecurrenceType = recurrenceType ?? current.RecurrenceType,
            DisplayOrder = displayOrder ?? current.DisplayOrder
        };

        await items.Update(itemId, request, user.UserId);

        return (await RequireItem(itemId)).Item;
    }

    [McpServerTool(Name = "delete_todo_item", Title = "Delete todo item", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes one item. Activities it logged are kept.")]
    public async Task<object> DeleteTodoItem([Description("Item id.")] int itemId)
    {
        var (item, _) = await RequireItem(itemId);
        await items.Delete(itemId, user.UserId);

        return new { deleted = true, itemId, title = item.Title };
    }

    [McpServerTool(Name = "complete_todo_item", Title = "Complete todo item", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Marks an item done at doneAt (default now). SIDE EFFECT: when the list has a completion tag, an activity with the item's TITLE as its value is logged to that tag (ResetIfExists reuses the day's row). There is no separate completion value. Fails with tag-day-locked when the tag is locked for that day.")]
    public async Task<TodoItemResponse> CompleteTodoItem(
        [Description("Item id.")] int itemId,
        [Description("yyyy-MM-ddTHH:mm in the user's local time, or ISO-8601 with offset; default now.")] string? doneAt = null)
    {
        var doneUtc = doneAt == null ? DateTime.UtcNow : DateArguments.ParseUtc(doneAt, "doneAt", await clock.Zone(user.UserId));

        return await items.Complete(itemId, new TodoItemCompleteRequest { DoneAt = doneUtc }, user.UserId);
    }

    [McpServerTool(Name = "reopen_todo_item", Title = "Reopen todo item", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Clears an item's done state. The activity that completion logged is NOT removed.")]
    public Task<TodoItemResponse> ReopenTodoItem([Description("Item id.")] int itemId) => items.Reopen(itemId, user.UserId);

    [McpServerTool(Name = "skip_todo_item", Title = "Skip todo item", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Marks a recurring (Daily/Weekly) item skipped for a date (default today); a one-off item (None) cannot be skipped — complete or delete it. Unlike reminders, nothing is logged to the tag.")]
    public async Task<TodoItemResponse> SkipTodoItem(
        [Description("Item id.")] int itemId,
        [Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        return await items.Skip(itemId, user.UserId, date == null ? null : DateArguments.ParseDate(date, "date"));
    }

    [McpServerTool(Name = "unskip_todo_item", Title = "Unskip todo item", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Clears an item's skipped state.")]
    public Task<TodoItemResponse> UnskipTodoItem([Description("Item id.")] int itemId) => items.Unskip(itemId, user.UserId);

    [McpServerTool(Name = "reorder_todo_items", Title = "Reorder todo items", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Sets the display order of items within a list. Items not listed keep their order.")]
    public async Task<TodoListResponse> ReorderTodoItems(
        [Description("List id.")] int listId,
        [Description("Item ids with their new positions.")] List<ReorderItem> orderedItems)
    {
        if (orderedItems.Count == 0)
        {
            throw new ArgumentException("orderedItems must not be empty.");
        }

        await items.Reorder(listId, orderedItems.Select(i => new TodoItemReorderRequest { Id = i.Id, DisplayOrder = i.DisplayOrder }).ToList(), user.UserId);

        return await lists.GetById(listId, user.UserId);
    }

    // --- helpers ---

    /// <summary>Items have no owner-scoped GetById on the service; the lists carry them.</summary>
    private async Task<(TodoItemResponse Item, TodoListResponse List)> RequireItem(int itemId)
    {
        foreach (var list in await lists.GetAll(user.UserId, await clock.Today(user.UserId)))
        {
            var item = list.Items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                return (item, list);
            }
        }

        throw new KeyNotFoundException("Todo item not found");
    }

    private async Task<DateOnly> ParseOrToday(string? date)
    {
        return date == null ? await clock.Today(user.UserId) : DateArguments.ParseDate(date, "date");
    }

    private static string RequireText(string value, string argument)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{argument} is required.");
        }

        return value.Trim();
    }
}
