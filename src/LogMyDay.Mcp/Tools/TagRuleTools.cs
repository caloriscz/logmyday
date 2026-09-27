using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Tag Activity Relations rules, read only. A rule calculates its target tag's daily value as the
/// sum of each source tag's values that day multiplied by its factor. The target is a computed
/// tag: its values are generated (activities with <c>generated: true</c>) and log/update/delete
/// tools refuse it. Rules are created and edited in the app.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TagRuleTools(McpUserContext user, ITagRuleService rules)
{
    [McpServerTool(Name = "list_tag_rules", Title = "List tag rules", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's rules: each computed (target) tag with its source tags and factors, whether the rule is active, the day it is active from, and how many values it has calculated.")]
    public Task<IList<TagRuleResponse>> ListTagRules() => rules.GetAll(user.UserId);

    [McpServerTool(Name = "get_tag_rule", Title = "Get tag rule", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one rule with its source tags and factors. Target value per day = Σ (source value × factor); a day without source values has no value.")]
    public Task<TagRuleResponse> GetTagRule([Description("Rule id.")] int ruleId) => rules.GetById(ruleId, user.UserId);
}
