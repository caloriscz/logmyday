using System.ComponentModel;
using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Reminders are the user's daily/weekly checklist, usually medication. Completing or skipping one
/// can write an activity to its completion tag, so those side effects are spelled out in the
/// descriptions. The service returns every reminder whatever its monitoring window; the list tool
/// applies the window the way the UI does unless asked not to.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class ReminderTools(
    McpUserContext user,
    IReminderService reminders,
    ITagService tags,
    ITagOptionListService optionLists,
    UserClock clock)
{
    public sealed record ReorderItem([property: Description("Reminder id.")] int Id, [property: Description("New position; lower first.")] int DisplayOrder);

    [McpServerTool(Name = "list_reminders", Title = "List reminders", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists reminders with their done/skipped state for a date (default today in the user's time zone), ordered by notify time. Reminders outside their monitoring window are hidden unless includeOutOfWindow is true. isSkipped is also true while the completion tag is day-locked.")]
    public async Task<IList<ReminderResponse>> ListReminders(
        [Description("yyyy-MM-dd; default today.")] string? date = null,
        [Description("Also return reminders whose monitor window does not cover the date.")] bool includeOutOfWindow = false)
    {
        var day = await ParseOrToday(date);
        var all = await reminders.GetAll(user.UserId, day);

        return includeOutOfWindow ? all : all.Where(r => r.IsWithinMonitoringWindow(day)).ToList();
    }

    [McpServerTool(Name = "get_reminder", Title = "Get reminder", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one reminder with its state for a date (default today).")]
    public async Task<ReminderResponse> GetReminder(
        [Description("Reminder id.")] int reminderId,
        [Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        return await Require(reminderId, await ParseOrToday(date));
    }

    [McpServerTool(Name = "create_reminder", Title = "Create reminder", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a reminder. recurrenceType None is stored as Daily. A completionTagId makes complete_reminder log an activity to that tag (autoLogMode Add appends; ResetIfExists replaces the day's row). The monitor window (monitorFromDate/monitorToDate) is when the reminder is active.")]
    public async Task<ReminderResponse> CreateReminder(
        [Description("Title, e.g. \"Vitamin D 2000 IU\".")] string title,
        [Description("Notes; also the logged value when complete_reminder gets no completionValue.")] string? notes = null,
        [Description("Time of day to notify, HH:mm.")] string? notifyAt = null,
        [Description("Daily (default) or Weekly. None is coerced to Daily.")] RecurrenceType recurrenceType = RecurrenceType.Daily,
        [Description("Add (default) or ResetIfExists.")] AutoLogMode autoLogMode = AutoLogMode.Add,
        [Description("Tag to log to on completion (must belong to the user).")] int? completionTagId = null,
        [Description("First active day, yyyy-MM-dd.")] string? monitorFromDate = null,
        [Description("Last active day, yyyy-MM-dd.")] string? monitorToDate = null,
        [Description("Allow completing without a value.")] bool allowUnfilled = false,
        [Description("Position among reminders with the same notify time.")] int displayOrder = 0)
    {
        if (completionTagId is int tagId)
        {
            await tags.GetTagById(tagId, user.UserId);
        }

        var request = new ReminderRequest
        {
            Title = RequireTitle(title),
            Notes = notes,
            NotifyAt = notifyAt == null ? null : DateArguments.ParseTime(notifyAt, "notifyAt"),
            RecurrenceType = recurrenceType,
            AutoLogMode = autoLogMode,
            CompletionTagId = completionTagId,
            MonitorFromDate = monitorFromDate == null ? null : DateArguments.ParseDate(monitorFromDate, "monitorFromDate"),
            MonitorToDate = monitorToDate == null ? null : DateArguments.ParseDate(monitorToDate, "monitorToDate"),
            AllowUnfilled = allowUnfilled,
            DisplayOrder = displayOrder
        };

        return await reminders.Create(request, user.UserId);
    }

    [McpServerTool(Name = "update_reminder", Title = "Update reminder", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a reminder; omitted arguments keep their current value. Pass 0 for completionTagId to detach the tag and \"\" for notifyAt, monitorFromDate or monitorToDate to clear them. recurrenceType None is coerced to Daily.")]
    public async Task<ReminderResponse> UpdateReminder(
        [Description("Reminder id.")] int reminderId,
        string? title = null,
        string? notes = null,
        [Description("HH:mm, or \"\" to clear.")] string? notifyAt = null,
        RecurrenceType? recurrenceType = null,
        AutoLogMode? autoLogMode = null,
        [Description("Tag id, or 0 to detach.")] int? completionTagId = null,
        [Description("yyyy-MM-dd, or \"\" to clear.")] string? monitorFromDate = null,
        [Description("yyyy-MM-dd, or \"\" to clear.")] string? monitorToDate = null,
        bool? allowUnfilled = null,
        int? displayOrder = null)
    {
        var today = await clock.Today(user.UserId);
        var current = await Require(reminderId, today);

        var newTagId = completionTagId switch { null => current.CompletionTagId, 0 => null, _ => completionTagId };
        if (newTagId is int tagId && tagId != current.CompletionTagId)
        {
            await tags.GetTagById(tagId, user.UserId);
        }

        var request = new ReminderRequest
        {
            Title = title == null ? current.Title : RequireTitle(title),
            Notes = notes ?? current.Notes,
            NotifyAt = notifyAt switch { null => current.NotifyAt, "" => null, _ => DateArguments.ParseTime(notifyAt, "notifyAt") },
            RecurrenceType = recurrenceType ?? current.RecurrenceType,
            AutoLogMode = autoLogMode ?? current.AutoLogMode,
            CompletionTagId = newTagId,
            MonitorFromDate = monitorFromDate switch { null => current.MonitorFromDate, "" => null, _ => DateArguments.ParseDate(monitorFromDate, "monitorFromDate") },
            MonitorToDate = monitorToDate switch { null => current.MonitorToDate, "" => null, _ => DateArguments.ParseDate(monitorToDate, "monitorToDate") },
            AllowUnfilled = allowUnfilled ?? current.AllowUnfilled,
            DisplayOrder = displayOrder ?? current.DisplayOrder
        };

        await reminders.Update(reminderId, request, user.UserId);

        return await Require(reminderId, today);
    }

    [McpServerTool(Name = "delete_reminder", Title = "Delete reminder", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a reminder and its per-day history. Activities it logged are kept.")]
    public async Task<object> DeleteReminder([Description("Reminder id.")] int reminderId)
    {
        var reminder = await Require(reminderId, await clock.Today(user.UserId));
        await reminders.Delete(reminderId, user.UserId);

        return new { deleted = true, reminderId, title = reminder.Title };
    }

    [McpServerTool(Name = "complete_reminder", Title = "Complete reminder", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Marks a reminder done for the day containing doneAt (default now). SIDE EFFECT: when the reminder has a completion tag, an activity is logged to it — completionValue (encoded for the tag) or, if omitted, the reminder's notes as the value; autoLogMode ResetIfExists replaces that day's row instead of adding. A backfilled doneAt in the past is fine. Fails with tag-day-locked when the tag is locked for that day.")]
    public async Task<ReminderResponse> CompleteReminder(
        [Description("Reminder id.")] int reminderId,
        [Description("When it was done: yyyy-MM-ddTHH:mm in the user's local time, or ISO-8601 with offset; default now.")] string? doneAt = null,
        [Description("Value to log to the completion tag (number, string or boolean).")] JsonElement? completionValue = null)
    {
        var zone = await clock.Zone(user.UserId);
        var doneUtc = doneAt == null ? DateTime.UtcNow : DateArguments.ParseUtc(doneAt, "doneAt", zone);
        var reminder = await Require(reminderId, DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(doneUtc, zone)));

        string? encoded = null;
        if (completionValue is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } value)
        {
            if (reminder.CompletionTagId is not int tagId)
            {
                throw new ArgumentException($"Reminder '{reminder.Title}' has no completion tag, so completionValue has nowhere to go.");
            }

            var tag = await tags.GetTagById(tagId, user.UserId);
            var options = tag.OptionListId is int listId ? await optionLists.GetById(listId, user.UserId) : null;
            encoded = ValueEncoder.Encode(tag, value, options);
        }

        return await reminders.Complete(reminderId, new ReminderCompleteRequest { DoneAt = doneUtc, CompletionValue = encoded }, user.UserId);
    }

    [McpServerTool(Name = "reopen_reminder", Title = "Reopen reminder", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Clears the done state for today. The activity that completion logged is NOT removed; use delete_activity for that.")]
    public Task<ReminderResponse> ReopenReminder([Description("Reminder id.")] int reminderId)
    {
        return reminders.Reopen(reminderId, user.UserId);
    }

    [McpServerTool(Name = "skip_reminder", Title = "Skip reminder", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Marks a reminder skipped for a date (default today) — \"not taken on purpose\". SIDE EFFECT: when the reminder has a completion tag, a zero/false activity is logged to it for that day so the record shows the skip.")]
    public async Task<ReminderResponse> SkipReminder(
        [Description("Reminder id.")] int reminderId,
        [Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        return await reminders.Skip(reminderId, user.UserId, await ParseOrToday(date));
    }

    [McpServerTool(Name = "unskip_reminder", Title = "Unskip reminder", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Clears the skipped state for a date (default today). The zero/false activity skip_reminder logged is NOT removed.")]
    public async Task<ReminderResponse> UnskipReminder(
        [Description("Reminder id.")] int reminderId,
        [Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        return await reminders.Unskip(reminderId, user.UserId, await ParseOrToday(date));
    }

    [McpServerTool(Name = "reorder_reminders", Title = "Reorder reminders", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Sets the display order of the given reminders (ties within the same notify time). Reminders not listed keep their order.")]
    public async Task<IList<ReminderResponse>> ReorderReminders([Description("Reminder ids with their new positions.")] List<ReorderItem> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("items must not be empty.");
        }

        await reminders.Reorder(items.Select(i => new ReminderReorderRequest { Id = i.Id, DisplayOrder = i.DisplayOrder }).ToList(), user.UserId);

        return await reminders.GetAll(user.UserId, await clock.Today(user.UserId));
    }

    // --- helpers ---

    private async Task<ReminderResponse> Require(int reminderId, DateOnly date)
    {
        var all = await reminders.GetAll(user.UserId, date);

        return all.FirstOrDefault(r => r.Id == reminderId) ?? throw new KeyNotFoundException("Reminder not found");
    }

    private async Task<DateOnly> ParseOrToday(string? date)
    {
        return date == null ? await clock.Today(user.UserId) : DateArguments.ParseDate(date, "date");
    }

    private static string RequireTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("The title is required.");
        }

        return title.Trim();
    }
}
