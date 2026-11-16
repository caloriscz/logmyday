using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Tag Activity Relations triggers, read only. A trigger watches one tag and, on a day its
/// condition holds, creates one reminder to log another tag. Triggers are created and edited in
/// the app; the reminders they create are ordinary reminders, listed by the reminder tools.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TagTriggerTools(McpUserContext user, ITagTriggerService triggers)
{
    private const string Semantics = "When the first tag's condition holds on a day (IsLogged, ignoring skip markers such as 0 or no; or Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual against the operand on the day value; or IsYes, IsNo), the trigger creates one reminder for that day to log the prompt tag (a tag or a record type): Timing AfterEntry rings offsetMinutes after the latest real entry of the first tag, AtTime at atTime (that day, or the next if the time has passed). A reminder time already past creates nothing. The reminder is withdrawn when the condition stops holding and closes itself when the prompt tag is logged after it was set. openReminderCount counts reminders today or later that are neither done nor skipped.";

    [McpServerTool(Name = "list_tag_triggers", Title = "List tag triggers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's triggers: name, whether it is active, the watched tag with its condition, the prompt tag and the timing. " + Semantics)]
    public Task<IList<TagTriggerResponse>> ListTagTriggers() => triggers.GetAll(user.UserId);

    [McpServerTool(Name = "get_tag_trigger", Title = "Get tag trigger", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one trigger. " + Semantics)]
    public Task<TagTriggerResponse> GetTagTrigger([Description("Trigger id.")] int triggerId) => triggers.GetById(triggerId, user.UserId);
}
