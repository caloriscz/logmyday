using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>Tag groups only organise tags; deleting one detaches its tags and destroys nothing else.</summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TagGroupTools(McpUserContext user, ITagGroupService groups)
{
    private Guid UserId => user.UserId;

    [McpServerTool(Name = "list_tag_groups", Title = "List tag groups", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's tag groups.")]
    public Task<IList<TagGroupResponse>> ListTagGroups() => groups.GetAll(UserId);

    [McpServerTool(Name = "get_tag_group", Title = "Get tag group", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one tag group.")]
    public Task<TagGroupResponse> GetTagGroup([Description("Group id.")] int groupId) => groups.GetById(groupId, UserId);

    [McpServerTool(Name = "create_tag_group", Title = "Create tag group", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a tag group. Tags in a group are addressed as \"group:name\".")]
    public async Task<TagGroupResponse> CreateTagGroup(
        [Description("Group name.")] string name,
        [Description("Optional description.")] string? description = null,
        [Description("Sort position among groups.")] int? displayOrder = null)
    {
        var id = await groups.Create(new TagGroupRequest { Name = Required(name), Description = description, DisplayOrder = displayOrder }, UserId);

        return await groups.GetById(id, UserId);
    }

    [McpServerTool(Name = "update_tag_group", Title = "Update tag group", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a tag group; omitted arguments keep their current value.")]
    public async Task<TagGroupResponse> UpdateTagGroup(
        [Description("Group id.")] int groupId,
        string? name = null,
        string? description = null,
        int? displayOrder = null)
    {
        var current = await groups.GetById(groupId, UserId);
        var request = new TagGroupRequest
        {
            Name = name == null ? current.Name : Required(name),
            Description = description ?? current.Description,
            DisplayOrder = displayOrder ?? current.DisplayOrder
        };

        await groups.Update(groupId, request, UserId);

        return await groups.GetById(groupId, UserId);
    }

    [McpServerTool(Name = "delete_tag_group", Title = "Delete tag group", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a tag group. Its tags are kept and become ungrouped; no activities are touched.")]
    public async Task<object> DeleteTagGroup([Description("Group id.")] int groupId)
    {
        var group = await groups.GetById(groupId, UserId);
        await groups.Delete(groupId, UserId);

        return new { deleted = true, groupId, name = group.Name };
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
