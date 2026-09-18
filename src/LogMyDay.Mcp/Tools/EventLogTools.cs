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
/// The event log is the user's diagnostic trail (and, under the Mcp category, this server's audit
/// trail). Agents may add their own entries, prefixed so they never pass as audit rows.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class EventLogTools(McpUserContext user, IEventLogService events)
{
    public const string AgentPrefix = "Agent: ";
    public const string PurgeSentinel = "PURGE_EVENTS";
    public const int MaxMessageLength = 500 - 7; // the service truncates at 500; leave room for the prefix
    public const int MaxDetailLength = 8000;

    [McpServerTool(Name = "query_event_log", Title = "Query event log", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Pages through the user's event log, newest first by default. Filter by level, text, a UTC date-time range and category (Mcp = this server's audit rows, ReminderDiag = mobile reminder diagnostics, Activity/Reminder/TodoList = app events). The detail column is included only for admin users.")]
    public async Task<PagedResult<EventLogResponse>> QueryEventLog(
        [Description("Info or Error.")] EventLogLevel? level = null,
        [Description("Text the message must contain.")] string? messageContains = null,
        [Description("Earliest CreatedUtc, ISO-8601.")] string? from = null,
        [Description("Latest CreatedUtc, ISO-8601.")] string? to = null,
        [Description("All (default), ReminderDiag, NoDiagnostics, Activity, Reminder, TodoList or Mcp.")] EventLogCategoryFilter category = EventLogCategoryFilter.All,
        [Description("time (default), level or message.")] string? sortBy = null,
        [Description("Newest/highest first (default true).")] bool sortDesc = true,
        [Description("1-based page number.")] int? page = null,
        [Description("Rows per page, max 200.")] int? pageSize = null)
    {
        var size = PageLimits.Clamp(pageSize);

        return await events.GetPaged(PageLimits.Page(page), size, user.UserId, user.IsAdmin, level, messageContains,
            ParseUtc(from, "from"), ParseUtc(to, "to"), sortBy ?? "time", sortDesc, category);
    }

    [McpServerTool(Name = "count_events", Title = "Count events", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Counts the user's event log rows matching the same filters as query_event_log.")]
    public async Task<object> CountEvents(
        EventLogLevel? level = null,
        string? messageContains = null,
        [Description("Earliest CreatedUtc, ISO-8601.")] string? from = null,
        [Description("Latest CreatedUtc, ISO-8601.")] string? to = null,
        EventLogCategoryFilter category = EventLogCategoryFilter.All)
    {
        var count = await events.GetCount(user.UserId, level, messageContains, ParseUtc(from, "from"), ParseUtc(to, "to"), category);

        return new { count, level, category };
    }

    [McpServerTool(Name = "record_event", Title = "Record event", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Writes an entry to the user's event log on the agent's behalf — for observations worth keeping, e.g. \"Agent: logged 3 items from voice note\". The message is stored with an \"Agent: \" prefix; it is not an audit row.")]
    public async Task<object> RecordEvent(
        [Description("Message, up to 493 characters.")] string message,
        [Description("Info (default) or Error.")] EventLogLevel level = EventLogLevel.Info,
        [Description("Optional longer detail, up to 8000 characters.")] string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("message is required.");
        }

        var text = message.Trim();
        if (text.Length > MaxMessageLength)
        {
            throw new ArgumentException($"message must be at most {MaxMessageLength} characters.");
        }

        if (detail?.Length > MaxDetailLength)
        {
            throw new ArgumentException($"detail must be at most {MaxDetailLength} characters.");
        }

        var stored = text.StartsWith(AgentPrefix, StringComparison.Ordinal) ? text : AgentPrefix + text;
        await events.Log(user.UserId, level, stored, detail);

        return new { recorded = true, message = stored, level };
    }

    [McpServerTool(Name = "purge_event_log", Title = "Purge event log", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes the user's event log rows older than olderThanDays. Without olderThanDays EVERY row goes, including this server's audit trail, and confirm must then be exactly \"PURGE_EVENTS\".")]
    public async Task<object> PurgeEventLog(
        [Description("Delete rows older than this many days; omit to delete all.")] int? olderThanDays = null,
        [Description("Required only when olderThanDays is omitted: PURGE_EVENTS.")] string? confirm = null)
    {
        if (olderThanDays is < 0)
        {
            throw new ArgumentException("olderThanDays must be zero or more.");
        }

        var before = await events.GetCount(user.UserId, dateTo: olderThanDays is int days ? DateTime.UtcNow.AddDays(-days) : null);
        if (olderThanDays == null)
        {
            ConfirmSentinel.Require(confirm, PurgeSentinel, $"Deletes all {before} event log rows of this user, audit rows included.");
        }

        await events.DeleteEvents(user.UserId, olderThanDays);

        return new { purged = true, olderThanDays, rowsDeleted = before };
    }

    private static DateTime? ParseUtc(string? text, string argument)
    {
        return text == null ? null : DateArguments.ParseUtc(text, argument, TimeZoneInfo.Utc);
    }
}
