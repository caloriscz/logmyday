using System.ComponentModel;
using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Contracts;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Activities are the logged rows. <c>log_value</c> is the tool an agent should reach for when
/// the user says "log 2000 IU of vitamin D": it resolves the tag, encodes the value for its input
/// type and applies the tag's accumulation rule. The lower-level tools mirror the REST surface.
/// Every tag id is resolved through the owner-scoped tag service first, because the activity
/// service itself does not check whose tag it is.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class ActivityTools(
    McpUserContext user,
    IActivityService activities,
    ITagService tags,
    TagLookup lookup,
    ITagOptionListService optionLists,
    UserClock clock)
{
    public const int MaxRowsPerYear = 5000;
    public const string ActionCreated = "created";
    public const string ActionAccumulated = "accumulated";
    public const string ActionReplaced = "replaced";

    [McpServerTool(Name = "list_activities", Title = "List activities", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists logged activities, newest first by default, paged. Filter by tag (id, or the find_tag syntax in tag), an inclusive date range (from/to as yyyy-MM-dd) and text the value must contain.")]
    public async Task<PagedResult<ActivitySummary>> ListActivities(
        [Description("Tag id.")] int? tagId = null,
        [Description("Tag by name (find_tag syntax); ignored when tagId is given.")] string? tag = null,
        [Description("First day, inclusive (yyyy-MM-dd).")] string? from = null,
        [Description("Last day, inclusive (yyyy-MM-dd).")] string? to = null,
        [Description("Text the stored value must contain.")] string? valueContains = null,
        [Description("desc (default), asc, group-asc or group-desc.")] string? orderBy = null,
        [Description("1-based page number.")] int? page = null,
        [Description("Rows per page, max 200.")] int? pageSize = null)
    {
        var resolvedTagId = await ResolveTagId(tagId, tag);
        var start = from == null ? (DateTime?)null : DateArguments.ParseDate(from, "from").ToDateTime(TimeOnly.MinValue);
        var end = to == null ? (DateTime?)null : DateArguments.EndOfDay(DateArguments.ParseDate(to, "to"));
        var size = PageLimits.Clamp(pageSize);

        var result = await activities.GetPaged(PageLimits.Page(page), size, orderBy ?? "desc", user.UserId, resolvedTagId, start, end, valueContains);

        return new PagedResult<ActivitySummary>
        {
            Items = result.Items.Select(ActivitySummary.From).ToList(),
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = size
        };
    }

    [McpServerTool(Name = "get_activity", Title = "Get activity", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one activity with its tag name, input type and value.")]
    public Task<ActivityResponse> GetActivity([Description("Activity id.")] int activityId)
    {
        return activities.GetById(activityId, user.UserId);
    }

    [McpServerTool(Name = "log_value", Title = "Log value", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Logs a value for a tag the easy way: resolves the tag (id, name, \"group:name\" or \":name\"), encodes the value for the tag's input type (see get_tag.valueEncoding) and applies the tag's rules. mode \"add\" (default) creates a row, or for a non-repeatable numeric tag adds to the period's existing value (action \"accumulated\"). mode \"set\" replaces the period's existing value on a non-repeatable tag (action \"replaced\"). Returns the resulting activity, the action taken and the tag. Fails with tag-day-locked when the user has locked that tag for the day — do not unlock on your own.")]
    public async Task<object> LogValue(
        [Description("Tag id, name, \"group:name\" or \":name\".")] string tag,
        [Description("The value: number, string or boolean, as the tag's input type expects.")] JsonElement value,
        [Description("When it happened, yyyy-MM-ddTHH:mm local; default now in the user's time zone.")] string? dateTime = null,
        [Description("\"add\" (default) or \"set\".")] string? mode = null)
    {
        var resolved = await lookup.Require(tag, user.UserId);
        var encoded = await Encode(resolved, value);
        var at = await ParseOrNow(dateTime);
        var replace = ParseMode(mode);

        if (replace && resolved.IsRepeatable)
        {
            throw new ArgumentException($"Tag '{resolved.Title}' is repeatable, so there is no single value to set; use mode \"add\", or update_activity on a specific row.");
        }

        var request = new ActivityRequest { PrimaryTagId = resolved.Id, Description = encoded, DateStarted = at };
        var existing = resolved.IsRepeatable || resolved.TimeGranularity == TimeGranularity.Exact
            ? null
            : await ExistingInPeriod(resolved, at);

        ActivityResponse activity;
        string action;
        if (replace && existing != null)
        {
            activity = await activities.Update(existing.Id, request, user.UserId);
            action = ActionReplaced;
        }
        else
        {
            activity = await activities.Create(request, user.UserId);
            action = existing != null && activity.Id == existing.Id ? ActionAccumulated : ActionCreated;
        }

        return new { activity = ActivitySummary.From(activity), action, tag = resolved.Title, storedValue = activity.Description };
    }

    [McpServerTool(Name = "create_activity", Title = "Create activity", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates an activity row for a tag id with an already-encoded value (prefer log_value). Non-repeatable numeric tags accumulate within their period instead of adding a row; the response says whether that happened.")]
    public async Task<object> CreateActivity(
        [Description("Tag id (must belong to the user).")] int tagId,
        [Description("Value, encoded for the tag's input type. Optional for tags that take no value.")] JsonElement? value = null,
        [Description("Start, yyyy-MM-ddTHH:mm local; default now in the user's time zone.")] string? dateTime = null,
        [Description("End, for range tags only.")] string? dateFinished = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);
        var at = await ParseOrNow(dateTime);
        var request = new ActivityRequest
        {
            PrimaryTagId = tag.Id,
            Description = HasValue(value) ? await Encode(tag, value!.Value) : null,
            DateStarted = at,
            DateFinished = dateFinished == null ? null : DateArguments.ParseDateTime(dateFinished, "dateFinished", await clock.Zone(user.UserId))
        };

        var existing = tag.IsRepeatable || tag.TimeGranularity == TimeGranularity.Exact ? null : await ExistingInPeriod(tag, at);
        var activity = await activities.Create(request, user.UserId);

        return new { activity, wasAccumulated = existing != null && activity.Id == existing.Id };
    }

    [McpServerTool(Name = "update_activity", Title = "Update activity", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates an activity; omitted arguments keep their current value. The value is re-encoded for the (possibly new) tag's input type.")]
    public async Task<ActivityResponse> UpdateActivity(
        [Description("Activity id.")] int activityId,
        [Description("New value.")] JsonElement? value = null,
        [Description("New start, yyyy-MM-ddTHH:mm local.")] string? dateTime = null,
        [Description("New end (range tags).")] string? dateFinished = null,
        [Description("Move the row to another tag id.")] int? tagId = null)
    {
        var current = await activities.GetById(activityId, user.UserId);
        var tag = await tags.GetTagById(tagId ?? current.PrimaryTagId ?? 0, user.UserId);
        var zone = await clock.Zone(user.UserId);

        var request = new ActivityRequest
        {
            PrimaryTagId = tag.Id,
            Description = HasValue(value) ? await Encode(tag, value!.Value) : current.Description,
            DateStarted = dateTime == null ? current.DateStarted : DateArguments.ParseDateTime(dateTime, "dateTime", zone),
            DateFinished = dateFinished == null ? current.DateFinished : DateArguments.ParseDateTime(dateFinished, "dateFinished", zone)
        };

        return await activities.Update(activityId, request, user.UserId);
    }

    [McpServerTool(Name = "delete_activity", Title = "Delete activity", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes one activity row.")]
    public async Task<object> DeleteActivity([Description("Activity id.")] int activityId)
    {
        if (!await activities.Delete(activityId, user.UserId))
        {
            throw new KeyNotFoundException("Activity not found");
        }

        return new { deleted = true, activityId };
    }

    [McpServerTool(Name = "check_duplicate", Title = "Check duplicate", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Whether the tag already has a row in the period (per its time granularity) containing dateTime. Useful before logging to a non-repeatable tag.")]
    public async Task<object> CheckDuplicate(
        [Description("Tag id.")] int tagId,
        [Description("yyyy-MM-ddTHH:mm local; default now.")] string? dateTime = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);
        var at = await ParseOrNow(dateTime);
        var exists = await activities.HasActivityForTimeGranularity(tag.Id, at, user.UserId);
        var period = DateArguments.PeriodOf(tag.TimeGranularity, at);

        return new { tagId = tag.Id, tag = tag.Title, exists, timeGranularity = tag.TimeGranularity, periodStart = period.Start, periodEnd = period.End };
    }

    [McpServerTool(Name = "get_period_sum", Title = "Get period sum", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("For numeric tags: the sum already logged in the period containing dateTime, the tag's maximum and the remaining capacity; for non-repeatable numeric tags also the existing row's value and id.")]
    public async Task<PeriodSumResponse> GetPeriodSum(
        [Description("Tag id.")] int tagId,
        [Description("yyyy-MM-ddTHH:mm local; default now.")] string? dateTime = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);

        return await activities.GetPeriodSum(tag.Id, await ParseOrNow(dateTime), user.UserId);
    }

    [McpServerTool(Name = "list_available_years", Title = "List available years", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Years that have at least one activity, optionally for one tag.")]
    public async Task<List<int>> ListAvailableYears([Description("Tag id.")] int? tagId = null)
    {
        if (tagId is int id)
        {
            await tags.GetTagById(id, user.UserId);
        }

        return await activities.GetAvailableYears(user.UserId, tagId);
    }

    [McpServerTool(Name = "list_activities_by_year", Title = "List activities by year", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description($"Every activity of a year (optionally one tag) in one call, for offline analysis. Refuses more than {MaxRowsPerYearText} rows — narrow by tag or use list_activities with a date range instead.")]
    public async Task<object> ListActivitiesByYear(
        [Description("Calendar year.")] int year,
        [Description("Tag id.")] int? tagId = null)
    {
        if (tagId is int id)
        {
            await tags.GetTagById(id, user.UserId);
        }

        var start = new DateTime(year, 1, 1);
        var count = (await activities.GetPaged(1, 1, "desc", user.UserId, tagId, start, start.AddYears(1).AddTicks(-1))).TotalCount;
        if (count > MaxRowsPerYear)
        {
            throw new ArgumentException($"{count} activities in {year} exceed the {MaxRowsPerYear}-row limit of this tool; filter by tagId, or page through list_activities with from/to.");
        }

        var rows = await activities.GetByYear(year, user.UserId, tagId);

        return new { year, tagId, count = rows.Count, items = rows.Select(ActivitySummary.From).ToList() };
    }

    private const string MaxRowsPerYearText = "5000";

    // --- helpers ---

    private async Task<int?> ResolveTagId(int? tagId, string? tag)
    {
        if (tagId is int id)
        {
            return (await tags.GetTagById(id, user.UserId)).Id;
        }

        return string.IsNullOrWhiteSpace(tag) ? null : (await lookup.Require(tag, user.UserId)).Id;
    }

    private async Task<string> Encode(TagResponse tag, JsonElement value)
    {
        var options = tag.OptionListId is int listId ? await optionLists.GetById(listId, user.UserId) : null;

        return ValueEncoder.Encode(tag, value, options);
    }

    private async Task<DateTime> ParseOrNow(string? dateTime)
    {
        return dateTime == null
            ? await clock.Now(user.UserId)
            : DateArguments.ParseDateTime(dateTime, "dateTime", await clock.Zone(user.UserId));
    }

    private static bool HasValue(JsonElement? value) => value is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) };

    private static bool ParseMode(string? mode)
    {
        return mode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "add" => false,
            "set" => true,
            _ => throw new ArgumentException($"mode must be \"add\" or \"set\", got '{mode}'.")
        };
    }

    /// <summary>The row a non-repeatable tag already has in the period around <paramref name="at"/>.</summary>
    private async Task<ActivityResponse?> ExistingInPeriod(TagResponse tag, DateTime at)
    {
        var (start, end) = DateArguments.PeriodOf(tag.TimeGranularity, at);
        var page = await activities.GetPaged(1, 1, "asc", user.UserId, tag.Id, start, end);

        return page.Items.FirstOrDefault();
    }
}
