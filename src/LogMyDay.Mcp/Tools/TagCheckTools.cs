using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Tag Activity Relations checks, read only. A check says what should hold on a day: if the first
/// tag meets its condition, the second tag meets its expectation. It writes nothing; the review
/// lists the days that break it. Checks are created and edited in the app.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TagCheckTools(McpUserContext user, ITagCheckService checks)
{
    private const string Semantics = "Kinds: IfThen; NotOnTheSameDay (if is IsLogged, then is IsNotLogged); Requires (both IsLogged). Operators: IsLogged, IsNotLogged (then side only), Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual (against the operand), IsYes, IsNo. A tag's day value is the day total for integer/decimal tags, the day average for ratings, scores and percentages, and the latest entry for yes/no, text and option-list tags. IsLogged ignores skip markers (0, or no on a yes/no tag). A day without the first tag, or where its condition does not hold, is not checked; a value expectation on a day without the second tag is met. Severity: Flag (review only) or Warn.";

    [McpServerTool(Name = "list_tag_checks", Title = "List tag checks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's checks: name, kind, severity, whether it is active, and both sides (tag, operator, operand). " + Semantics)]
    public Task<IList<TagCheckResponse>> ListTagChecks() => checks.GetAll(user.UserId);

    [McpServerTool(Name = "get_tag_check", Title = "Get tag check", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one check. " + Semantics)]
    public Task<TagCheckResponse> GetTagCheck([Description("Check id.")] int checkId) => checks.GetById(checkId, user.UserId);

    [McpServerTool(Name = "review_tag_checks", Title = "Review tag checks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the days in a range that break an active check, newest first, with both tags' day values (null = not logged that day). A Requires-style expectation (then IsLogged) on today is pending, not broken: the day is not over. Defaults to the last 30 days; the range ends today at the latest and spans at most 366 days.")]
    public Task<TagCheckReviewResponse> ReviewTagChecks(
        [Description("First day, inclusive (yyyy-MM-dd).")] string? from = null,
        [Description("Last day, inclusive (yyyy-MM-dd).")] string? to = null)
    {
        var start = from == null ? (DateOnly?)null : DateArguments.ParseDate(from, "from");
        var end = to == null ? (DateOnly?)null : DateArguments.ParseDate(to, "to");

        return checks.Review(start, end, user.UserId);
    }
}
