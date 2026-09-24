using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Option lists constrain a text tag to a fixed set of values. Activities store the option's
/// value, so removing or renaming an option never rewrites old rows.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class OptionListTools(McpUserContext user, ITagOptionListService optionLists)
{
    public sealed record OptionInput(
        [property: Description("Id of an existing option to keep (update_option_list only); omit for a new option.")] int? Id,
        [property: Description("Stored value.")] string Value,
        [property: Description("Optional label shown instead of the value.")] string? DisplayName);

    [McpServerTool(Name = "list_option_lists", Title = "List option lists", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the option lists the user can use — personal ones and global ones — with their options.")]
    public Task<IEnumerable<TagOptionListResponse>> ListOptionLists() => optionLists.GetAll(user.UserId);

    [McpServerTool(Name = "get_option_list", Title = "Get option list", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one option list with its options.")]
    public Task<TagOptionListResponse> GetOptionList([Description("Option list id.")] int optionListId) => optionLists.GetById(optionListId, user.UserId);

    [McpServerTool(Name = "create_option_list", Title = "Create option list", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates an option list. isGlobal makes it visible to every user and needs an admin.")]
    public async Task<TagOptionListResponse> CreateOptionList(
        [Description("List name.")] string name,
        [Description("The options, in order.")] List<OptionInput> options,
        [Description("Shared with all users (admin only).")] bool isGlobal = false)
    {
        RequireGlobalPermission(isGlobal);
        var request = new TagOptionListRequest { Name = Required(name), IsGlobal = isGlobal, Options = ToRequests(options, allowIds: false) };

        var id = await optionLists.Create(request, user.UserId);

        return await optionLists.GetById(id, user.UserId);
    }

    [McpServerTool(Name = "update_option_list", Title = "Update option list", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a personal option list. When options is given it REPLACES the whole set: an option whose id is included is kept (its value/label updated), one whose id is omitted is deleted, one without an id is added. Omit options to keep them all.")]
    public async Task<TagOptionListResponse> UpdateOptionList(
        [Description("Option list id.")] int optionListId,
        string? name = null,
        [Description("The complete new set of options; see the tool description.")] List<OptionInput>? options = null)
    {
        var current = await optionLists.GetById(optionListId, user.UserId);
        var request = new TagOptionListRequest
        {
            Name = name == null ? current.Name : Required(name),
            IsGlobal = current.IsGlobal,
            Options = options == null
                ? current.Options.Select(o => new TagOptionRequest { Id = o.Id, Value = o.Value, DisplayName = o.DisplayName }).ToList()
                : ToRequests(options, allowIds: true)
        };

        await optionLists.Update(optionListId, request, user.UserId);

        return await optionLists.GetById(optionListId, user.UserId);
    }

    [McpServerTool(Name = "delete_option_list", Title = "Delete option list", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a personal option list. Fails with conflict while a tag still uses it; detach the tag first (update_tag optionListId 0).")]
    public async Task<object> DeleteOptionList([Description("Option list id.")] int optionListId)
    {
        var list = await optionLists.GetById(optionListId, user.UserId);
        await optionLists.Delete(optionListId, user.UserId);

        return new { deleted = true, optionListId, name = list.Name };
    }

    private void RequireGlobalPermission(bool isGlobal)
    {
        if (isGlobal && !user.IsAdmin)
        {
            throw new UnauthorizedAccessException("Only an admin can create a global option list.");
        }
    }

    private static List<TagOptionRequest> ToRequests(List<OptionInput> options, bool allowIds)
    {
        if (options.Count == 0)
        {
            throw new ArgumentException("At least one option is required.");
        }

        var requests = new List<TagOptionRequest>(options.Count);
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Value))
            {
                throw new ArgumentException("Every option needs a value.");
            }

            requests.Add(new TagOptionRequest { Id = allowIds ? option.Id : null, Value = option.Value.Trim(), DisplayName = option.DisplayName });
        }

        var duplicate = requests.GroupBy(r => r.Value, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Option value '{duplicate.Key}' appears more than once.");
        }

        return requests;
    }

    private static string Required(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The name is required.");
        }

        return name.Trim();
    }
}
