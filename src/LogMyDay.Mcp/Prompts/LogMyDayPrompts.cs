using System.ComponentModel;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Prompts;

/// <summary>
/// Ready-made instructions for the three things an agent is most often asked to do. The wording
/// was approved by the owner (2026-09-18); the templates are string constants so the contract test
/// can check that every tool they name exists.
/// </summary>
[McpServerPromptType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class LogMyDayPrompts(McpUserContext user, UserClock clock)
{
    public const string DailyReviewTemplate =
        "Review my LogMyDay entries for {date}. First call server_info for my time zone and culture, then " +
        "list_activities with from and to both set to {date}, list_reminders for {date} (in-window only, the " +
        "default), and list_todo_lists for {date}. Summarise what I logged, grouped by tag group, showing values " +
        "with units. List the reminders and todo items still open, and point out required tags with nothing " +
        "logged that day. Keep it under 200 words and give no advice unless I ask. If something looks mislogged " +
        "— an impossible value, a duplicate — mention it but do not change anything.";

    public const string WeeklySummaryTemplate =
        "Produce a weekly summary for the seven days ending {weekEnding}. {tagSelection} then call " +
        "get_activity_summary with bucket Day for each, from {weekStart} to {weekEnding}. For numeric tags report " +
        "total, average, minimum, maximum and the current streak; for Yes/No tags the number of days done; for " +
        "text tags the most frequent values. Compare with the previous seven days by calling get_activity_summary " +
        "again for {previousStart} to {previousEnd} and state the direction of change in one phrase per tag. " +
        "Finish with the two tags that changed most. Use numbers only from tool results; never estimate.";

    public const string WeeklyTagsGiven = "Resolve each of these tags with find_tag: {tags},";

    public const string WeeklyTagsDefault =
        "Call list_tags to see every tag, keep the ones that have data in the week (get_activity_summary count > 0),";

    public const string LogByConversationTemplate =
        "I will describe things I did or measured in plain language. For each item: resolve the tag with find_tag " +
        "— ask me if the result is ambiguous or has no match — read the tag's input type and unit from the result, " +
        "convert my wording to the tag's value encoding, and call log_value with mode add and the date and time I " +
        "stated (default now). Before calling log_value for a non-repeatable numeric tag, tell me the value will be " +
        "added to that day's existing total unless I say \"set\". After each call confirm in one line: tag, value " +
        "with unit, date and time, and whether it was created, accumulated or replaced. If the result is " +
        "tag-day-locked, tell me and stop rather than unlocking.";

    [McpServerPrompt(Name = "daily_review", Title = "Daily review")]
    [Description("Summarise one day's entries, open reminders and todos, and required tags left empty; changes nothing.")]
    public async Task<string> DailyReview(
        [Description("yyyy-MM-dd; default today in the user's time zone.")] string? date = null)
    {
        var day = await ResolveDate(date, "date");

        return DailyReviewTemplate.Replace("{date}", day.ToString("yyyy-MM-dd"));
    }

    [McpServerPrompt(Name = "weekly_summary", Title = "Weekly summary")]
    [Description("Per-tag statistics for the seven days ending on a date, compared with the seven days before.")]
    public async Task<string> WeeklySummary(
        [Description("Last day of the week, yyyy-MM-dd; default today in the user's time zone.")] string? weekEnding = null,
        [Description("Comma-separated tags (find_tag syntax); default every tag with data in the week.")] string? tags = null)
    {
        var end = await ResolveDate(weekEnding, "weekEnding");
        var selection = string.IsNullOrWhiteSpace(tags)
            ? WeeklyTagsDefault
            : WeeklyTagsGiven.Replace("{tags}", string.Join(", ", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

        return WeeklySummaryTemplate
            .Replace("{tagSelection}", selection)
            .Replace("{weekStart}", end.AddDays(-6).ToString("yyyy-MM-dd"))
            .Replace("{weekEnding}", end.ToString("yyyy-MM-dd"))
            .Replace("{previousStart}", end.AddDays(-13).ToString("yyyy-MM-dd"))
            .Replace("{previousEnd}", end.AddDays(-7).ToString("yyyy-MM-dd"));
    }

    [McpServerPrompt(Name = "log_by_conversation", Title = "Log by conversation")]
    [Description("Turn plain-language statements into log_value calls, confirming each entry and never unlocking a locked day.")]
    public string LogByConversation() => LogByConversationTemplate;

    private async Task<DateOnly> ResolveDate(string? text, string argument)
    {
        return string.IsNullOrWhiteSpace(text) ? await clock.Today(user.UserId) : DateArguments.ParseDate(text, argument);
    }
}
