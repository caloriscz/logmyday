using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>The fixed catalogue of input types, each with the rule for encoding a value.</summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class InputTypeTools(IInputTypeService inputTypes)
{
    [McpServerTool(Name = "list_input_types", Title = "List input types", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the input types a tag can have, with which range/step/repeatable settings each allows and how a value is encoded for it.")]
    public async Task<object> ListInputTypes()
    {
        var types = await inputTypes.GetAllInputTypes();

        return types.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            t.IsRangeEditable,
            t.IsMinimumEditable,
            t.IsMaximumEditable,
            t.IsStepEditable,
            t.IsRepeatableEditable,
            ValueEncoding = ValueEncoder.Describe(t.Id)
        }).ToList();
    }
}
