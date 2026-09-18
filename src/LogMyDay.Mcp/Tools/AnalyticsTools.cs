using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>The one server-side analytics tool of v1; multi-tag comparison is a v2 item.</summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class AnalyticsTools(McpUserContext user, IActivitySummaryService summaries, TagLookup lookup, UserClock clock)
{
    public const int DefaultRangeDays = 30;

    [McpServerTool(Name = "get_activity_summary", Title = "Get activity summary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Statistics for one tag over an inclusive date range (default the last 30 days ending today in the user's time zone): count, days with data, sum/min/max/average (booleans: true count and true share), first/last, current and longest streak, one row per day/week/month bucket including empty ones, and top values for text tags. Rows are bucketed by their stored date (convention \"stored-local-date\"), exactly as the app's Insights show them. Report numbers from this result only.")]
    public async Task<ActivitySummaryResponse> GetActivitySummary(
        [Description("Tag id, name, \"group:name\" or \":name\".")] string tag,
        [Description("First day, yyyy-MM-dd; default to − 29 days.")] string? from = null,
        [Description("Last day, yyyy-MM-dd; default today.")] string? to = null,
        [Description("Day (default), Week or Month. Day allows up to 400 days, Week 1100, Month 3700.")] SummaryBucket bucket = SummaryBucket.Day)
    {
        var resolved = await lookup.Require(tag, user.UserId);
        var end = to == null ? await clock.Today(user.UserId) : DateArguments.ParseDate(to, "to");
        var start = from == null ? end.AddDays(-(DefaultRangeDays - 1)) : DateArguments.ParseDate(from, "from");

        var request = new ActivitySummaryRequest { TagId = resolved.Id, From = start, To = end, Bucket = bucket };

        return await summaries.Summarize(request, user.UserId);
    }
}
