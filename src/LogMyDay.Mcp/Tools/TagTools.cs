using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Contracts;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Tags are what everything is logged against. Reads return compact summaries; <c>get_tag</c>
/// returns the whole configuration plus how to encode a value for it. Deleting a tag cascades to
/// every activity ever logged for it, so it is guarded by a sentinel and reports the count first.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class TagTools(
    McpUserContext user,
    ITagService tags,
    TagLookup lookup,
    IInputTypeService inputTypes,
    IActivityService activities,
    ITagGroupService groups,
    ITagOptionListService optionLists,
    IColorSchemeService colorSchemes,
    IUnitService units)
{
    public const string DeleteSentinelPrefix = "DELETE_TAG_";

    [McpServerTool(Name = "list_tags", Title = "List tags", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's tags as compact summaries, paged. filter matches the tag name (contains by default; filterType \"exact\" for an exact name). groupId narrows to one group; -1 means ungrouped tags only.")]
    public async Task<PagedResult<TagSummary>> ListTags(
        [Description("Text the tag name must contain (or equal, with filterType \"exact\").")] string? filter = null,
        [Description("\"contains\" (default) or \"exact\".")] string? filterType = null,
        [Description("Only tags in this group; -1 for ungrouped tags.")] int? groupId = null,
        [Description("asc (default), desc, group-asc or group-desc.")] string? orderBy = null,
        [Description("1-based page number.")] int? page = null,
        [Description("Rows per page, max 200.")] int? pageSize = null)
    {
        var size = PageLimits.Clamp(pageSize);
        var result = await tags.GetPaged(PageLimits.Page(page), size, orderBy ?? "asc", user.UserId, filter, filterType, groupId);
        var names = await InputTypeNames();

        return new PagedResult<TagSummary>
        {
            Items = result.Items.Select(t => TagSummary.From(t, names)).ToList(),
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = size
        };
    }

    [McpServerTool(Name = "get_tag", Title = "Get tag", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns a tag's full configuration (input type, unit, range, repeatability, granularity, group, option list with its options, colour scheme) and the rule for encoding a value for it.")]
    public async Task<object> GetTag([Description("Tag id.")] int tagId)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);

        return await Detail(tag);
    }

    [McpServerTool(Name = "find_tag", Title = "Find tag", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Resolves a tag from what the user said. query may be an id, a plain name (exact, then unique starts-with, then unique contains), \"group:name\" for a grouped tag, or \":name\" for an ungrouped one. Returns match (null when none or ambiguous) and candidates.")]
    public async Task<object> FindTag([Description("Id, name, \"group:name\" or \":name\".")] string query)
    {
        var result = await lookup.Find(query, user.UserId);
        var names = await InputTypeNames();

        return new
        {
            match = result.Match == null ? null : TagSummary.From(result.Match, names),
            ambiguous = result.IsAmbiguous,
            candidates = result.Candidates.Select(c => TagSummary.From(c, names)).ToList()
        };
    }

    [McpServerTool(Name = "create_tag", Title = "Create tag", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a tag. inputTypeId is one of server_info's inputTypes (1 Integer, 2 String, 3 Boolean, 4 Date, 5 Time, 6 Decimal, 7 Star 0-5, 8 Star 0-10, 9 Percentage, 10 Score 0-5, 11 Score 0-10). Non-repeatable numeric tags accumulate values within a period instead of adding rows. Referenced group, option list and colour scheme must belong to the user; units are shared.")]
    public async Task<object> CreateTag(
        [Description("Tag name (without the group prefix).")] string name,
        [Description("Input type id.")] int inputTypeId,
        [Description("Optional description.")] string? description = null,
        [Description("Group id.")] int? groupId = null,
        [Description("Unit id (units are global).")] int? unitId = null,
        [Description("Option list id; the value must then be one of its options.")] int? optionListId = null,
        [Description("Colour scheme id.")] int? colorSchemeId = null,
        [Description("Whether several rows per period are allowed (default true). Non-repeatable numeric tags accumulate.")] bool isRepeatable = true,
        [Description("Whether the day review flags the tag when nothing is logged (default false).")] bool isRequired = false,
        [Description("Period for duplicate/accumulation rules: Exact, Hourly, Daily (default), Weekly, Monthly, Yearly.")] TimeGranularity timeGranularity = TimeGranularity.Daily,
        [Description("Whether logging a range (start and end) is enabled.")] bool isRange = false,
        [Description("Minimum value for numeric types.")] double? minValue = null,
        [Description("Maximum value for numeric types (per period for accumulating tags).")] double? maxValue = null,
        [Description("Step for numeric types.")] double? step = null,
        [Description("Default value, encoded as the input type stores it.")] string? defaultValue = null)
    {
        await RequireReferences(groupId, optionListId, colorSchemeId, unitId);

        var request = new TagRequest
        {
            Tag = RequireName(name),
            Description = description,
            TypeId = inputTypeId,
            GroupId = groupId,
            UnitId = unitId,
            OptionListId = optionListId,
            ColorSchemeId = colorSchemeId,
            IsRepeatable = isRepeatable,
            IsRequired = isRequired,
            TimeGranularity = timeGranularity,
            IsRange = isRange,
            MinValue = minValue,
            MaxValue = maxValue,
            Step = step,
            DefaultValue = defaultValue
        };

        var id = await tags.Create(request, user.UserId);

        return await Detail(await tags.GetTagById(id, user.UserId));
    }

    [McpServerTool(Name = "update_tag", Title = "Update tag", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a tag; omitted arguments keep their current value. Pass 0 for groupId, unitId, optionListId or colorSchemeId to detach. Changing the input type does not convert existing values.")]
    public async Task<object> UpdateTag(
        [Description("Tag id.")] int tagId,
        string? name = null,
        int? inputTypeId = null,
        string? description = null,
        [Description("Group id; 0 detaches.")] int? groupId = null,
        [Description("Unit id; 0 detaches.")] int? unitId = null,
        [Description("Option list id; 0 detaches.")] int? optionListId = null,
        [Description("Colour scheme id; 0 detaches.")] int? colorSchemeId = null,
        bool? isRepeatable = null,
        bool? isRequired = null,
        TimeGranularity? timeGranularity = null,
        bool? isRange = null,
        double? minValue = null,
        double? maxValue = null,
        double? step = null,
        string? defaultValue = null)
    {
        var current = await tags.GetTagById(tagId, user.UserId);

        var newGroup = Merge(groupId, current.GroupId);
        var newUnit = Merge(unitId, current.UnitId);
        var newOptionList = Merge(optionListId, current.OptionListId);
        var newColorScheme = Merge(colorSchemeId, current.ColorSchemeId);
        await RequireReferences(
            newGroup == current.GroupId ? null : newGroup,
            newOptionList == current.OptionListId ? null : newOptionList,
            newColorScheme == current.ColorSchemeId ? null : newColorScheme,
            newUnit == current.UnitId ? null : newUnit);

        var request = new TagRequest
        {
            Tag = name == null ? BareName(current) : RequireName(name),
            Description = description ?? current.Description,
            TypeId = inputTypeId ?? current.TypeId ?? 0,
            GroupId = newGroup,
            UnitId = newUnit,
            OptionListId = newOptionList,
            ColorSchemeId = newColorScheme,
            IsRepeatable = isRepeatable ?? current.IsRepeatable,
            IsRequired = isRequired ?? current.IsRequired,
            TimeGranularity = timeGranularity ?? current.TimeGranularity,
            IsRange = isRange ?? current.IsRange,
            MinValue = minValue ?? current.MinValue,
            MaxValue = maxValue ?? current.MaxValue,
            Step = step ?? current.Step,
            DefaultValue = defaultValue ?? current.DefaultValue
        };

        await tags.Update(tagId, request, user.UserId);

        return await Detail(await tags.GetTagById(tagId, user.UserId));
    }

    [McpServerTool(Name = "delete_tag", Title = "Delete tag", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a tag AND every activity ever logged for it, plus its scan mappings and day locks. Requires confirm = \"DELETE_TAG_<tagId>\". Without it, nothing is deleted and the response reports how many activities would go — tell the user before confirming.")]
    public async Task<object> DeleteTag(
        [Description("Tag id.")] int tagId,
        [Description("Must be exactly DELETE_TAG_<tagId>.")] string? confirm = null)
    {
        var tag = await tags.GetTagById(tagId, user.UserId);
        var activityCount = (await activities.GetPaged(1, 1, "desc", user.UserId, tagId)).TotalCount;

        ConfirmSentinel.Require(confirm, DeleteSentinelPrefix + tagId,
            $"Deletes tag {tagId} '{tag.Title}' and its {activityCount} activities, with any scan mappings and day locks.");

        await tags.Delete(tagId, user.UserId);

        return new { deleted = true, tagId, title = tag.Title, activitiesDeleted = activityCount };
    }

    // --- helpers ---

    private async Task<object> Detail(TagResponse tag)
    {
        var names = await InputTypeNames();
        TagOptionListResponse? options = tag.OptionListId is int listId ? await optionLists.GetById(listId, user.UserId) : null;

        return new
        {
            tag,
            summary = TagSummary.From(tag, names),
            valueEncoding = ValueEncoder.Describe(tag.TypeId),
            options = options?.Options.Select(o => new { o.Value, o.DisplayName }).ToList()
        };
    }

    private async Task<IReadOnlyDictionary<int, string>> InputTypeNames()
    {
        return (await inputTypes.GetAllInputTypes()).ToDictionary(t => t.Id, t => t.Name);
    }

    /// <summary>
    /// The tag service trusts foreign keys; resolving each through its owner-scoped service turns a
    /// wrong or foreign id into not-found before anything is written.
    /// </summary>
    private async Task RequireReferences(int? groupId, int? optionListId, int? colorSchemeId, int? unitId)
    {
        if (groupId is int g)
        {
            await groups.GetById(g, user.UserId);
        }

        if (optionListId is int o)
        {
            await optionLists.GetById(o, user.UserId);
        }

        if (colorSchemeId is int c)
        {
            await colorSchemes.GetById(c, user.UserId);
        }

        if (unitId is int u)
        {
            await units.GetById(u);
        }
    }

    private static int? Merge(int? requested, int? current) => requested switch
    {
        null => current,
        0 => null,
        _ => requested
    };

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The tag name is required.");
        }

        return name.Trim();
    }

    private static string BareName(TagResponse tag)
    {
        return tag.GroupName != null && tag.Title.StartsWith(tag.GroupName + ": ", StringComparison.Ordinal)
            ? tag.Title[(tag.GroupName.Length + 2)..]
            : tag.Title;
    }
}
