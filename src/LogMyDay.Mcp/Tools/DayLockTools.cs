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
/// A day lock is the user's "I'm done with this tag for today": logging to the tag on that day is
/// refused and reminders for it show as skipped. Unlocking is the user's decision — an agent that
/// hits tag-day-locked should ask, not unlock.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class DayLockTools(McpUserContext user, ITagDayLockService locks, ITagService tags, UserClock clock)
{
    [McpServerTool(Name = "list_tag_day_locks", Title = "List tag day locks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the lock rows for a date (default today in the user's time zone): which tags are locked or explicitly unlocked, who set it (User, Auto, System) and why.")]
    public async Task<IList<TagDayLockResponse>> ListTagDayLocks([Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        return await locks.GetForDate(user.UserId, await ParseOrToday(date));
    }

    [McpServerTool(Name = "get_tag_day_lock", Title = "Get tag day lock", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("The lock state of one tag on a date: locked, explicitly unlocked, or no row (which means not locked).")]
    public async Task<object> GetTagDayLock(
        [Description("Tag id.")] int tagId,
        [Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);
        var day = await ParseOrToday(date);
        var row = await locks.Find(user.UserId, tag.Id, day);

        return new
        {
            tagId = tag.Id,
            tag = tag.Title,
            date = day,
            isLocked = row?.IsLocked ?? false,
            hasRow = row != null,
            setBy = row?.SetBy,
            setAt = row?.SetAt,
            reason = row?.Reason
        };
    }

    [McpServerTool(Name = "set_tag_day_lock", Title = "Set tag day lock", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Locks or unlocks a tag for a date on the user's behalf (recorded as set by the user). Locking refuses further logging to the tag that day and marks its reminders skipped. Only unlock when the user asked for it.")]
    public async Task<TagDayLockResponse> SetTagDayLock(
        [Description("Tag id.")] int tagId,
        [Description("true to lock, false to record an explicit unlock.")] bool isLocked,
        [Description("yyyy-MM-dd; default today.")] string? date = null,
        [Description("Why, up to 200 characters.")] string? reason = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);
        if (reason?.Length > 200)
        {
            throw new ArgumentException("reason must be at most 200 characters.");
        }

        var request = new TagDayLockRequest { TagId = tag.Id, Date = await ParseOrToday(date), IsLocked = isLocked, Reason = reason };

        return await locks.Upsert(user.UserId, request, DayLockSetBy.User);
    }

    [McpServerTool(Name = "delete_tag_day_lock", Title = "Delete tag day lock", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Removes the lock row for a tag and date entirely, as if it had never been set. This differs from set_tag_day_lock with isLocked false, which keeps a row recording that the user chose to unlock (and stops auto-locks from re-locking that day).")]
    public async Task<object> DeleteTagDayLock(
        [Description("Tag id.")] int tagId,
        [Description("yyyy-MM-dd; default today.")] string? date = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);
        var day = await ParseOrToday(date);
        if (await locks.Find(user.UserId, tag.Id, day) == null)
        {
            throw new KeyNotFoundException($"No lock row for tag {tag.Id} on {day:yyyy-MM-dd}.");
        }

        await locks.Delete(user.UserId, tag.Id, day);

        return new { deleted = true, tagId = tag.Id, date = day };
    }

    private async Task<DateOnly> ParseOrToday(string? date)
    {
        return date == null ? await clock.Today(user.UserId) : DateArguments.ParseDate(date, "date");
    }
}
