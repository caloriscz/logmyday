using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Tag Activity Relations rules, read only. A rule calculates its target tag's daily value: a
/// weighted sum (Σ source value × factor), an aggregate (average, minimum, maximum of the day's
/// source values, or the count of the day's source entries), or a conditional value (the result
/// of the first case whose condition holds on the source's day value). The target is a computed
/// tag: its values are generated (activities with <c>generated: true</c>) and log/update/delete
/// tools refuse it. Rules are created and edited in the app.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TagRuleTools(McpUserContext user, ITagRuleService rules)
{
    [McpServerTool(Name = "list_tag_rules", Title = "List tag rules", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's rules: each computed (target) tag, the template (WeightedSum; Aggregate with aggregateKind Average, Minimum, Maximum or Count; or Conditional with ordered cases), its source tags and factors (weighted sum only), whether the rule is active, the day it is active from, and how many values it has calculated.")]
    public Task<IList<TagRuleResponse>> ListTagRules() => rules.GetAll(user.UserId);

    [McpServerTool(Name = "get_tag_rule", Title = "Get tag rule", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one rule with its template, source tags, factors and cases. WeightedSum: target per day = Σ (source value × factor). Aggregate: average, minimum or maximum of the day's source values, or the count of their entries. Conditional (one source, factors unused): the target gets the resultValue of the first case that holds; operators IsLogged, Otherwise (last only), Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual (against operand), IsYes, IsNo. The compared day value is the day total for integer/decimal sources, the day average for ratings, scores and percentages, and the latest entry for yes/no, text and option-list sources. A presence rule is a single IsLogged case. A day without source values, or where no case holds, has no value.")]
    public Task<TagRuleResponse> GetTagRule([Description("Rule id.")] int ruleId) => rules.GetById(ruleId, user.UserId);
}
